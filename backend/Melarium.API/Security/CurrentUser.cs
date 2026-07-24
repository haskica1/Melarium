using System.Security.Claims;
using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Enums;

namespace Melarium.API.Security;

/// <summary>
/// Resolves <see cref="ICurrentUser"/> from the request's JWT claims via <see cref="IHttpContextAccessor"/>.
/// All claim parsing is null-safe (never throws) so a malformed token degrades to "unauthenticated"
/// rather than a 500.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal? _principal;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _principal = accessor.HttpContext?.User;
    }

    public bool IsAuthenticated => _principal?.Identity?.IsAuthenticated ?? false;

    public int? UserId => ParseInt(_principal?.FindFirstValue(ClaimTypes.NameIdentifier));

    public int? OrganizationId => ParseInt(_principal?.FindFirstValue("organizationId"));

    public int? ApiaryId => ParseInt(_principal?.FindFirstValue("apiaryId"));

    public UserRole? Role =>
        Enum.TryParse<UserRole>(_principal?.FindFirstValue(ClaimTypes.Role), out var role)
            ? role
            : null;

    private static int? ParseInt(string? value) =>
        int.TryParse(value, out var parsed) ? parsed : null;
}
