using Melarium.Domain.Common;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// The downgrade-lock ranking rules (SPEC-24). This is the half of the feature that decides which of
/// a beekeeper's own hives they can still open, so the ordering has to be exact and stable: the same
/// organization must get the same answer on every request, or hives would flicker in and out.
/// </summary>
public class PlanLockPolicyTests
{
    private static readonly DateTime Day = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    private static PlanLockPolicy.ApiaryRow Apiary(int id, int dayOffset) =>
        new(id, Day.AddDays(dayOffset));

    private static PlanLockPolicy.BeehiveRow Hive(int id, int apiaryId, int dayOffset) =>
        new(id, apiaryId, Day.AddDays(dayOffset));

    // ── Nothing to lock ──────────────────────────────────────────────────────────

    [Fact]
    public void NoLimits_LocksNothing()
    {
        var result = PlanLockPolicy.Locked(
            [Apiary(1, 0), Apiary(2, 1)],
            [Hive(10, 1, 0), Hive(11, 2, 1)],
            maxApiaries: null, maxBeehives: null);

        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void WithinLimits_LocksNothing()
    {
        var result = PlanLockPolicy.Locked(
            [Apiary(1, 0)],
            [Hive(10, 1, 0), Hive(11, 1, 1)],
            maxApiaries: 1, maxBeehives: 7);

        Assert.True(result.IsEmpty);
    }

    // ── Oldest first ─────────────────────────────────────────────────────────────

    [Fact]
    public void ApiariesPastTheLimit_LockOldestFirst()
    {
        var result = PlanLockPolicy.Locked(
            [Apiary(3, 2), Apiary(1, 0), Apiary(2, 1)],   // deliberately out of order
            [],
            maxApiaries: 1, maxBeehives: null);

        Assert.Equal([2, 3], result.ApiaryIds.OrderBy(id => id));
    }

    [Fact]
    public void SameCreatedAt_BreaksTieOnId_SoTheOrderIsStable()
    {
        var result = PlanLockPolicy.Locked(
            [Apiary(7, 0), Apiary(4, 0), Apiary(9, 0)],   // identical timestamps
            [],
            maxApiaries: 2, maxBeehives: null);

        // 4 and 7 are the two oldest by id; 9 is the one that falls off — every time.
        Assert.Equal([9], result.ApiaryIds);
    }

    [Fact]
    public void HivesPastTheLimit_LockOldestFirstAcrossTheWholeOrganization()
    {
        // The quota is per organization, not per apiary — the same way the create-side gate counts.
        var result = PlanLockPolicy.Locked(
            [Apiary(1, 0), Apiary(2, 1)],
            [Hive(10, 1, 0), Hive(11, 2, 1), Hive(12, 1, 2), Hive(13, 2, 3)],
            maxApiaries: null, maxBeehives: 2);

        Assert.Equal([12, 13], result.BeehiveIds.OrderBy(id => id));
    }

    // ── The apiary lock cascades ─────────────────────────────────────────────────

    [Fact]
    public void HivesInALockedApiary_AreLockedWithIt()
    {
        var result = PlanLockPolicy.Locked(
            [Apiary(1, 0), Apiary(2, 1)],
            [Hive(10, 1, 0), Hive(20, 2, 1), Hive(21, 2, 2)],
            maxApiaries: 1, maxBeehives: null);

        Assert.Equal([2], result.ApiaryIds);
        Assert.Equal([20, 21], result.BeehiveIds.OrderBy(id => id));
    }

    [Fact]
    public void HivesInALockedApiary_DoNotConsumeTheHiveQuota()
    {
        // Free: 1 apiary, 7 hives. Apiary 2 (with 20 hives) is locked, so the 7 slots go entirely to
        // apiary 1 — otherwise the quota would be spent on hives that are unreachable anyway.
        var apiaries = new[] { Apiary(1, 0), Apiary(2, 1) };
        var hives = Enumerable.Range(1, 8).Select(i => Hive(100 + i, 1, i))
            .Concat(Enumerable.Range(1, 20).Select(i => Hive(200 + i, 2, 50 + i)))
            .ToArray();

        var result = PlanLockPolicy.Locked(apiaries, hives, maxApiaries: 1, maxBeehives: 7);

        // Apiary 1 keeps its 7 oldest; only its 8th is over the limit.
        Assert.DoesNotContain(101, result.BeehiveIds);
        Assert.DoesNotContain(107, result.BeehiveIds);
        Assert.Contains(108, result.BeehiveIds);

        // All 20 of the locked apiary's hives are locked, and 7 of apiary 1's survived.
        Assert.Equal(21, result.BeehiveIds.Count);
    }

    // ── The trial case that started this ─────────────────────────────────────────

    [Fact]
    public void TrialExpiry_ProToFree_LeavesOneApiaryAndSevenHives()
    {
        // 3 apiaries, 50 hives built up during the 30-day Pro trial; the plan falls to Free.
        var apiaries = new[] { Apiary(1, 0), Apiary(2, 1), Apiary(3, 2) };
        var hives = Enumerable.Range(0, 50)
            .Select(i => Hive(1000 + i, (i % 3) + 1, i))
            .ToArray();

        var result = PlanLockPolicy.Locked(apiaries, hives, maxApiaries: 1, maxBeehives: 7);

        Assert.Equal([2, 3], result.ApiaryIds.OrderBy(id => id));

        var reachable = hives.Where(h => !result.BeehiveIds.Contains(h.Id)).ToList();
        Assert.Equal(7, reachable.Count);
        Assert.All(reachable, h => Assert.Equal(1, h.ApiaryId));
    }
}
