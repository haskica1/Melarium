using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Security;

/// <summary>
/// Single source of truth for resource ownership / multi-tenant authorization.
/// Resolves a resource's owning organization and apiary, then applies the role rules:
/// <list type="bullet">
/// <item><description><c>SystemAdmin</c> — unrestricted (platform-wide).</description></item>
/// <item><description><c>OrgAdmin</c> — limited to resources in their organization.</description></item>
/// <item><description><c>ApiaryAdmin</c> — limited to resources in their assigned apiary.</description></item>
/// <item><description><c>Beekeeper</c> — limited to beehives explicitly assigned to them.</description></item>
/// </list>
/// The <c>Ensure*</c> methods throw <see cref="Common.Exceptions.ForbiddenAccessException"/> on denial.
/// </summary>
public interface IAccessGuard
{
    /// <summary>True when the current caller is a platform SystemAdmin.</summary>
    bool IsSystemAdmin { get; }

    /// <summary>Ensures the caller may act within the given organization.</summary>
    void EnsureInOrganization(int organizationId);

    /// <summary>
    /// Ensures the caller has management rights over the apiary (and, by extension, its beehives):
    /// SystemAdmin, the OrgAdmin of its organization, or the ApiaryAdmin assigned to it.
    /// </summary>
    Task EnsureCanManageApiaryAsync(int apiaryId);

    /// <summary>Synchronous management check when the apiary's organization is already known.</summary>
    void EnsureCanManageApiary(int apiaryId, int organizationId);

    /// <summary>
    /// Ensures the caller can access the beehive's data: management rights over its apiary,
    /// or a Beekeeper assigned to the beehive.
    /// </summary>
    Task EnsureCanAccessBeehiveAsync(int beehiveId);

    /// <summary>Non-throwing variant of <see cref="EnsureCanAccessBeehiveAsync"/>.</summary>
    Task<bool> CanAccessBeehiveAsync(int beehiveId);

    /// <summary>The set of beehive ids the current Beekeeper is assigned to (empty for other roles).</summary>
    Task<HashSet<int>> GetAssignedBeehiveIdsAsync();

    /// <summary>The set of apiary ids containing at least one beehive assigned to the current Beekeeper.</summary>
    Task<HashSet<int>> GetAssignedApiaryIdsAsync();
}
