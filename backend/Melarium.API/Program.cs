using System.Text;
using System.Threading.RateLimiting;
using Melarium.API.Middleware;
using Melarium.Application;
using Melarium.Application.Features.Ai;
using Melarium.Application.Features.Alerts;
using Melarium.Application.Features.Inspections;
using Melarium.Application.Features.Weather;
using Melarium.Entity;
using Melarium.Entity.Seed;
using Melarium.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration guards ──────────────────────────────────────────────────────
// Secrets are intentionally NOT committed to the repository. Locally they come from
// appsettings.Development.json / user-secrets; in production from environment variables
// (e.g. ConnectionStrings__DefaultConnection, Jwt__Secret). Fail fast with a clear
// message instead of a cryptic error deep in the request pipeline.
string RequireConfig(string key)
{
    var value = builder.Configuration[key];
    if (string.IsNullOrWhiteSpace(value))
        throw new InvalidOperationException(
            $"Configuration value '{key}' is missing. Set the environment variable " +
            $"'{key.Replace(":", "__")}' (or provide it via user-secrets / appsettings.Development.json).");
    return value;
}

RequireConfig("ConnectionStrings:DefaultConnection");
if (RequireConfig("Jwt:Secret").Length < 32)
    throw new InvalidOperationException("Jwt:Secret must be at least 32 characters long (HS256 key).");

// ── Services ───────────────────────────��─────────────────────────────────────

builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new Melarium.API.Middleware.UtcDateTimeJsonConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Melarium API",
        Version = "v1",
        Description = "REST API for managing beekeeping operations — apiaries, beehives, and inspections."
    });

    // JWT auth button in Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

// Register Application, persistence (Entity), and Infrastructure layers via extension methods
builder.Services.AddApplication();
builder.Services.AddEntity(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

// Current-user abstraction — resolves the authenticated caller from JWT claims for the
// Application layer's authorization (see ICurrentUser / IAccessGuard).
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Melarium.Application.Common.Interfaces.ICurrentUser, Melarium.API.Security.CurrentUser>();

// Weather service — typed HttpClient targeting Open-Meteo (free, no API key needed)
builder.Services.AddHttpClient<IWeatherService, WeatherService>(client =>
{
    client.BaseAddress = new Uri("https://api.open-meteo.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Shared Groq Whisper transcription (reused by voice inspections and the AI assistant).
builder.Services.AddHttpClient<ITranscriptionService, GroqTranscriptionService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

// Voice parsing service — Groq API (Whisper transcription + Llama field extraction);
// key configured via Groq:ApiKey. Longer timeout: audio upload + two model calls.
builder.Services.AddHttpClient<IVoiceParsingService, VoiceParsingService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

// Free-form Groq prose replies — originally the AI advisor's client (SPEC-01, retired into the
// assistant by SPEC-18); LearningTopicService also uses it to draft a topic's Bosnian summary.
builder.Services.AddHttpClient<IProseAiClient, GroqProseAiClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

// AI assistant command interpretation (SPEC-17/18) — Groq Llama constrained to a JSON envelope.
builder.Services.AddHttpClient<IAssistantAiClient, GroqAssistantAiClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

// Weekly AI summary — Groq chat (Llama), reuses Groq:ApiKey. Runs from the AlertScanWorker
// on Mondays; the typed HttpClient keeps the Groq call out of the request path.
builder.Services.AddHttpClient<IWeeklySummaryService, WeeklySummaryService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

// Frame photo AI analysis (SPEC-05 Phase 2) — Groq vision model, reuses Groq:ApiKey.
// Longer timeout: multi-MB base64 image upload + vision inference.
builder.Services.AddHttpClient<IPhotoAnalysisAiClient, GroqPhotoAnalysisAiClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(90);
});

// "Scan by number" fallback — Groq vision reads the hive's painted number/label when on-device
// OCR is not confident. Same Groq vision model + Groq:ApiKey as photo analysis.
builder.Services.AddHttpClient<IHiveNumberOcrClient, GroqHiveNumberOcrClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(90);
});

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

// CORS — origins are configured via AllowedOrigins in appsettings.json.
// On Render, override with the env var:  AllowedOrigins=https://your-app.vercel.app
var allowedOrigins = (builder.Configuration["AllowedOrigins"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Rate limiting — throttle login attempts per client IP to slow credential-stuffing/brute force.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Token refresh is a legitimate periodic operation — keep it separate from (and more
    // generous than) login so a burst of refreshes can't lock out sign-ins, and vice versa.
    options.AddPolicy("auth-refresh", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Self-service sign-up creates a new organisation each time, so throttle it per IP to
    // curb automated tenant/account spam.
    options.AddPolicy("register", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Endpoints that send mail to an address the caller names (password reset, resend
    // verification). Tighter than login: each accepted request puts a message in someone's
    // inbox, so this is an abuse budget, not a usability one. Kept separate from "login" so a
    // reset burst can never lock out sign-ins.
    options.AddPolicy("auth-email", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Redeeming an emailed token (reset / verify). The tokens are 256-bit so guessing is not the
    // threat — this just bounds automated hammering. More generous than auth-email because a
    // user legitimately retries a link.
    options.AddPolicy("auth-token", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Voice parsing calls a paid external API (Groq) per request — throttle so a single
    // client cannot burn through the quota.
    options.AddPolicy("voice-parse", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // AI advisor chat also hits the paid Groq API per message — same 10/min per-IP cap.
    options.AddPolicy("ai-chat", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Feedback submission (SPEC-13). Same reasoning and budget as auth-email: every accepted
    // request puts a message in the operator's inbox, so this bounds abuse, not usability.
    options.AddPolicy("feedback", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Photo AI analysis sends multi-MB images to the paid Groq vision model — tighter cap (SPEC-05).
    options.AddPolicy("photo-analyze", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// Reverse-proxy awareness. The API always runs behind a proxy that terminates TLS (nginx on the
// VPS, Render's edge), so the socket peer is the proxy — not the client. Without this every
// request looks like it comes from one address and the rate limiters above collapse into a
// single shared bucket for the whole platform (5 logins/min for *everyone*).
//
// KnownProxies/KnownNetworks are cleared because the proxy's address is not fixed (Docker bridge
// gateway, Render edge). That is safe only because the container is never exposed directly:
// docker-compose binds it to 127.0.0.1 and nginx is the sole entry point. If that ever changes,
// pin the proxy address here instead — otherwise clients could spoof X-Forwarded-For.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Health check — liveness probe at /health for the deployment platform.
builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Middleware Pipeline ────────────────────────────────��──────────────────────

// Restore the real client IP/scheme from the proxy's headers. Must run before anything that
// reads them — the rate limiter partitions on RemoteIpAddress.
app.UseForwardedHeaders();

// Global exception handler must be first after the proxy fix-up
app.UseGlobalExceptionHandling();

// Swagger is Development-only: in production it published the full API surface, every schema
// and every admin endpoint to anonymous callers.
// Registered BEFORE UseSecurityHeaders on purpose — Swagger's middleware short-circuits its own
// requests, so the restrictive `default-src 'none'` CSP below never applies to the Swagger UI
// (which loads its own CSS/JS). Do not reorder these two.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Melarium API v1"));
}

app.UseSecurityHeaders();

// HTTPS redirect only in local dev — Render (and most cloud hosts) terminate
// TLS at the proxy level; the container itself only serves plain HTTP.
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseCors("FrontendPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// ── Database Initialisation ───────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MelariumDbContext>();
    await db.Database.MigrateAsync();

    if (app.Environment.IsDevelopment())
    {
        // Demo accounts use well-known passwords committed to the (public) repo — dev only.
        await DatabaseInitializer.SeedUsersAsync(db);
        // Starter learning topics (SPEC-06) — production content is authored by SystemAdmin.
        await DatabaseInitializer.SeedLearningTopicsAsync(db);
    }
    else
    {
        // Production: demo accounts must be unusable; the real SystemAdmin is provisioned
        // from Bootstrap:SysAdminEmail / Bootstrap:SysAdminPassword environment variables.
        await DatabaseInitializer.LockDemoAccountsAsync(db);
        await DatabaseInitializer.EnsureBootstrapAdminAsync(
            db,
            app.Configuration["Bootstrap:SysAdminEmail"],
            app.Configuration["Bootstrap:SysAdminPassword"]);
    }
}

app.Run();
