using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Security;

namespace Melarium.API.Middleware;

/// <summary>
/// Read-only members (SPEC-24). When an organization falls to a plan with fewer member seats than it
/// has accounts, the extra members keep their sign-in and everything they could read, but lose every
/// write. Rather than adding a check to each service, this refuses the write at the edge: anything
/// that is not a safe HTTP method is a write, which is a rule no future endpoint can forget to follow.
///
/// It throws <see cref="PlanLimitException"/> instead of writing a response itself, so
/// <see cref="GlobalExceptionMiddleware"/> shapes the 402 exactly as every other plan refusal —
/// including the <c>code: "plan-limit"</c> marker the frontend's upsell interceptor keys on. That
/// requires it to sit inside the exception handler and after authentication, which is where
/// <c>Program.cs</c> registers it.
///
/// Locking the extra accounts out of the app entirely was the alternative; read-only was chosen
/// because a member is a person who did nothing wrong — the plan is the owner's to fix.
/// </summary>
public class ReadOnlyMemberMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Writes that stay open. Deliberately narrow, and each for a reason: a member who cannot mark a
    /// notification read watches a badge that never clears; one who cannot change their own password
    /// is a security problem, not a billing one; one who cannot sign out is trapped; and one who
    /// cannot file feedback has no way to tell anyone the app is refusing them.
    /// Own-profile deletion lives under /api/profile too, and staying is the member's choice to make.
    /// (SPEC-20 contact — WhatsApp, Viber, phone, e-mail — is frontend-only and needs nothing here.)
    /// </summary>
    private static readonly string[] AlwaysAllowed =
    [
        "/api/auth",
        "/api/profile",
        "/api/notifications",
        "/api/feedback",
    ];

    public ReadOnlyMemberMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IPlanLock planLock)
    {
        if (await ShouldRefuseAsync(context, planLock))
            throw new PlanLimitException(
                "Vaš nalog je u režimu samo za čitanje jer paket organizacije ne pokriva sve članove — vlasnik organizacije treba nadograditi paket da biste ponovo mogli unositi podatke.");

        await _next(context);
    }

    private static async Task<bool> ShouldRefuseAsync(HttpContext context, IPlanLock planLock)
    {
        if (IsSafeMethod(context.Request.Method)) return false;
        if (!context.Request.Path.StartsWithSegments("/api")) return false;
        if (context.User.Identity?.IsAuthenticated != true) return false;
        if (IsAlwaysAllowed(context.Request.Path)) return false;

        // Last, because it is the only check that touches the database.
        return await planLock.IsCurrentUserReadOnlyAsync();
    }

    private static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

    private static bool IsAlwaysAllowed(PathString path) =>
        AlwaysAllowed.Any(prefix => path.StartsWithSegments(prefix));
}

/// <summary>Extension method for clean middleware registration in Program.cs.</summary>
public static class ReadOnlyMemberMiddlewareExtensions
{
    public static IApplicationBuilder UseReadOnlyMemberGuard(this IApplicationBuilder app) =>
        app.UseMiddleware<ReadOnlyMemberMiddleware>();
}
