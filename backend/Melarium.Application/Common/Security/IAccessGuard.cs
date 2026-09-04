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
///
/// <para>
/// Since SPEC-24 the guard also applies the <b>downgrade lock</b> (<see cref="IPlanLock"/>): a row
/// the caller owns but that sits above their plan's limits is refused with
/// <see cref="Common.Exceptions.PlanLimitException"/> → 402, distinct from the 403 above so the UI
/// can offer an upgrade instead of "not yours". The two checks live together because everything
/// that reads apiary or hive data already passes through here — putting the lock in each service
/// instead would mean sixty places to keep in sync, and one missed call is a hole in the paywall.
/// </para>
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
    /// <param name="allowLocked">
    /// Skips the downgrade lock. Reserved for <b>deleting</b> the apiary: a locked apiary you cannot
    /// delete has no way out except paying, which would leave an organization permanently stuck.
    /// </param>
    Task EnsureCanManageApiaryAsync(int apiaryId, bool allowLocked = false);

    /// <summary>
    /// Synchronous management check when the apiary's organization is already known. Roles only —
    /// the downgrade lock needs a database read, so callers on a locked-sensitive path pair this
    /// with <see cref="IPlanLock.EnsureApiaryUnlockedAsync"/>.
    /// </summary>
    void EnsureCanManageApiary(int apiaryId, int organizationId);

    /// <summary>
    /// Non-throwing variant of <see cref="EnsureCanManageApiaryAsync"/>, mirroring
    /// <see cref="CanAccessBeehiveAsync"/>. Used where a denial is a normal outcome to report rather
    /// than an error to raise — the assistant's pre-flight, which drops an action it could not perform
    /// instead of offering the user a card that is certain to fail. False for a locked apiary too.
    /// </summary>
    Task<bool> CanManageApiaryAsync(int apiaryId);

    /// <summary>
    /// Ensures the caller can access the beehive's data: management rights over its apiary,
    /// or a Beekeeper assigned to the beehive.
    /// </summary>
    /// <param name="allowLocked">
    /// Skips the downgrade lock. Reserved for deleting a hive (the way out of a downgrade) and for
    /// recording an inspection, which is allowed to land on a locked hive so a round of offline
    /// inspections (SPEC-07) syncing after the plan changed is never lost — the data goes in, but
    /// stays unreadable until the plan is upgraded.
    /// </param>
    Task EnsureCanAccessBeehiveAsync(int beehiveId, bool allowLocked = false);

    /// <summary>Non-throwing variant of <see cref="EnsureCanAccessBeehiveAsync"/>; false when locked.</summary>
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
    /// <param name="includeLocked">
    /// Locked hives are excluded by default, so scanning, matching and the assistant cannot reach
    /// one. Only the hive list passes true: it still shows them, greyed out and flagged.
    /// </param>
    Task<IReadOnlyList<Beehive>> GetAccessibleBeehivesAsync(bool includeLocked = false);

    /// <summary>Every apiary the caller may see, role-scoped. Same purpose as <see cref="GetAccessibleBeehivesAsync"/>.</summary>
    Task<IReadOnlyList<Apiary>> GetAccessibleApiariesAsync(bool includeLocked = false);
}
