namespace Melarium.Domain.Common;

/// <summary>
/// Which apiaries and beehives a downgraded organization loses access to (SPEC-24). Pure and
/// deterministic: given the organization's rows and its effective plan's limits, it returns the
/// ids that are locked. Nothing is stored — the caller recomputes it, so an upgrade unlocks
/// everything by itself and deleting an active row promotes the next locked one.
///
/// The rule, in order:
/// <list type="number">
/// <item><description>Apiaries rank oldest-first; those past <c>MaxApiaries</c> are locked.</description></item>
/// <item><description>Every hive inside a locked apiary is locked with it, and does <b>not</b>
/// consume the hive quota — otherwise a beekeeper whose one reachable apiary holds 20 hives would
/// see the quota spent on hives they cannot open anyway.</description></item>
/// <item><description>Hives in the surviving apiaries rank oldest-first across all of them (the
/// quota is per organization, like the counter that enforces it on create); those past
/// <c>MaxBeehives</c> are locked.</description></item>
/// </list>
/// Merged-away hives (SPEC-19) must be filtered out by the caller before they get here — they
/// already do not count toward the plan limit, so they must not consume a slot either.
/// </summary>
public static class PlanLockPolicy
{
    /// <summary>Oldest-first, with the id as the tiebreaker so the order is stable across calls.</summary>
    private static IEnumerable<T> Ranked<T>(IEnumerable<T> rows, Func<T, DateTime> createdAt, Func<T, int> id) =>
        rows.OrderBy(createdAt).ThenBy(id);

    public static PlanLockResult Locked(
        IReadOnlyCollection<ApiaryRow> apiaries,
        IReadOnlyCollection<BeehiveRow> beehives,
        int? maxApiaries,
        int? maxBeehives)
    {
        // Unlimited on both counts (Max/Partner) — the common case, and no ordering work for it.
        if (maxApiaries is null && maxBeehives is null)
            return PlanLockResult.Empty;

        var lockedApiaries = maxApiaries is int apiaryLimit
            ? Ranked(apiaries, a => a.CreatedAt, a => a.Id).Skip(apiaryLimit).Select(a => a.Id).ToHashSet()
            : [];

        var reachable = beehives.Where(b => !lockedApiaries.Contains(b.ApiaryId)).ToList();

        var lockedBeehives = beehives
            .Where(b => lockedApiaries.Contains(b.ApiaryId))
            .Select(b => b.Id)
            .ToHashSet();

        if (maxBeehives is int beehiveLimit)
            lockedBeehives.UnionWith(
                Ranked(reachable, b => b.CreatedAt, b => b.Id).Skip(beehiveLimit).Select(b => b.Id));

        return new PlanLockResult(lockedApiaries, lockedBeehives);
    }

    public readonly record struct ApiaryRow(int Id, DateTime CreatedAt);
    public readonly record struct BeehiveRow(int Id, int ApiaryId, DateTime CreatedAt);
}

/// <summary>The locked id sets for one organization. Empty means the plan locks nothing.</summary>
public sealed record PlanLockResult(IReadOnlySet<int> ApiaryIds, IReadOnlySet<int> BeehiveIds)
{
    public static readonly PlanLockResult Empty = new(new HashSet<int>(), new HashSet<int>());

    public bool IsEmpty => ApiaryIds.Count == 0 && BeehiveIds.Count == 0;
}
