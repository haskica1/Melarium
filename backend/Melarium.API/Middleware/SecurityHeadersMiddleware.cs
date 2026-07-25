namespace Melarium.API.Middleware;

/// <summary>
/// Adds baseline security response headers. This API serves JSON and streamed photos — never HTML
/// an attacker could get a browser to execute — so the policy is deliberately restrictive.
/// The SPA itself is served by nginx, which carries its own headers (see deploy/nginx.melarium.conf.example).
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Never let a browser second-guess our Content-Type (JSON sniffed as HTML = stored XSS).
        headers["X-Content-Type-Options"] = "nosniff";
        // No API response is ever meant to be framed.
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        // The API needs none of these device capabilities.
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        // Belt-and-braces for anything a browser might render directly (e.g. an error page):
        // deny every resource type and re-state the framing ban in CSP terms.
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";

        return _next(context);
    }
}

/// <summary>Extension method for clean middleware registration in Program.cs.</summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();
}
