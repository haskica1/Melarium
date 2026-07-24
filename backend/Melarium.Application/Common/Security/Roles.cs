using Melarium.Domain.Enums;

namespace Melarium.Application.Common.Security;

/// <summary>
/// Compile-time role name constants for <c>[Authorize(Roles = ...)]</c> attributes.
/// Derived from <see cref="UserRole"/> via <c>nameof</c> so they always match the JWT role claim
/// (which is produced by <c>UserRole.ToString()</c>) and update automatically if the enum is renamed.
/// </summary>
public static class Roles
{
    public const string SystemAdmin = nameof(UserRole.SystemAdmin);
    public const string OrganizationAdmin = nameof(UserRole.OrganizationAdmin);
    public const string ApiaryAdmin = nameof(UserRole.ApiaryAdmin);
    public const string Beekeeper = nameof(UserRole.Beekeeper);

    /// <summary>Apiary-level managers and above (excludes plain beekeepers).</summary>
    public const string Managers = ApiaryAdmin + "," + OrganizationAdmin + "," + SystemAdmin;

    /// <summary>Organization-level managers and above.</summary>
    public const string OrgManagers = OrganizationAdmin + "," + SystemAdmin;
}
