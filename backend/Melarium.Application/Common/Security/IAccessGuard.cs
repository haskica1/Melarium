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
    /// Non-throwing variant of <see cref="EnsureCanManageApiaryAsync"/>, mirroring
    /// <see cref="CanAccessBeehiveAsync"/>. Used where a denial is a normal outcome to report rather
    /// than an error to raise — the assistant's pre-flight, which drops an action it could not perform
    /// instead of offering the user a card that is certain to fail.
    /// </summary>
    Task<bool> CanManageApiaryAsync(int apiaryId);

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

    /// <summary>
    /// Every beehive the caller may see, role-scoped. Shared by the hive list, number matching and the
    /// AI assistant's target resolution (SPEC-17) — the assistant searches this set and nothing else,
    /// which is what makes an out-of-scope hive unreachable by construction rather than by a check.
    /// </summary>
    Task<IReadOnlyList<Beehive>> GetAccessibleBeehivesAsync();

    /// <summary>Every apiary the caller may see, role-scoped. Same purpose as <see cref="GetAccessibleBeehivesAsync"/>.</summary>
    Task<IReadOnlyList<Apiary>> GetAccessibleApiariesAsync();
}
