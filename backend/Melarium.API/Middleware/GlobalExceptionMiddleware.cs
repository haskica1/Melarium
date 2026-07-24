using System.Net;
using System.Text.Json;
using Melarium.Application.Common.Exceptions;
using ValidationException = Melarium.Application.Common.Exceptions.ValidationException;

namespace Melarium.API.Middleware;

/// <summary>
/// Centralized exception handling middleware.
/// Catches known application exceptions and maps them to appropriate HTTP responses.
/// Unknown exceptions return 500 to avoid leaking implementation details.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex, _env.IsDevelopment());
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, bool includeDetails)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, title, errors) = exception switch
        {
            NotFoundException nfe => (
                HttpStatusCode.NotFound,
                "Resource Not Found",
                new Dictionary<string, string[]> { ["detail"] = [nfe.Message] }
            ),
            UnauthorizedException ue => (
                HttpStatusCode.Unauthorized,
                "Unauthorized",
                new Dictionary<string, string[]> { ["detail"] = [ue.Message] }
            ),
            ForbiddenAccessException fae => (
                HttpStatusCode.Forbidden,
                "Access Denied",
                new Dictionary<string, string[]> { ["detail"] = [fae.Message] }
            ),
            ValidationException ve => (
                HttpStatusCode.BadRequest,
                "Validation Failed",
                ve.Errors
            ),
            BusinessRuleException bre => (
                HttpStatusCode.UnprocessableEntity,
                "Business Rule Violation",
                new Dictionary<string, string[]> { ["detail"] = [bre.Message] }
            ),
            // SPEC-09: plan limits are 402 (not 403) so the frontend renders an upgrade
            // prompt instead of "access denied". See decisions.md.
            PlanLimitException ple => (
                HttpStatusCode.PaymentRequired,
                "Payment Required",
                new Dictionary<string, string[]> { ["detail"] = [ple.Message] }
            ),
            // Exception type/message/inner details are only exposed in Development —
            // in production they can leak table names, connection info, etc.
            _ when includeDetails => (
                HttpStatusCode.InternalServerError,
                "An unexpected error occurred",
                new Dictionary<string, string[]>
                {
                    ["detail"] = [$"{exception.GetType().Name}: {exception.Message}"],
                    ["innerException"] = [exception.InnerException?.Message ?? "none"],
                    ["innerInnerException"] = [exception.InnerException?.InnerException?.Message ?? "none"]
                }
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                "An unexpected error occurred",
                new Dictionary<string, string[]>
                {
                    ["detail"] = ["An unexpected error occurred. Please try again later."]
                }
            )
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            type = $"https://httpstatuses.com/{(int)statusCode}",
            title,
            status = (int)statusCode,
            // Machine-readable marker the frontend's upsell interceptor keys on (SPEC-09);
            // null (and omitted from JSON) for every other exception type.
            code = exception is PlanLimitException ? "plan-limit" : null,
            errors
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        await context.Response.WriteAsync(json);
    }
}

/// <summary>Extension method for clean middleware registration in Program.cs.</summary>
public static class ExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<GlobalExceptionMiddleware>();
}
