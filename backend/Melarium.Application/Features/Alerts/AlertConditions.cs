using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Alerts;

/// <summary>
/// The conditions behind the alert rules, shared by the daily scan (which notifies) and the dashboard
/// (which shows the same state live), so the two can never disagree about what "overdue" means.
/// </summary>
public static class AlertConditions
{
    /// <summary>The last two inspections decrease, and the latest is already Low.</summary>
    public static bool IsHoneyDropping(HoneyLevel latest, HoneyLevel previous) =>
        (int)latest < (int)previous && latest == HoneyLevel.Low;

    /// <summary>Days strips have been in, when that is at least <paramref name="removalDays"/>; otherwise null.</summary>
    public static int? StripsOverdueDays(Treatment t, DateTime now, int removalDays)
    {
        if (t.Method != ApplicationMethod.Strips || t.EndDate is not null) return null;
        var days = (int)(now - t.StartDate).TotalDays;
        return days >= removalDays ? days : null;
    }

    /// <summary>
    /// The earliest pending round at least <paramref name="overdueDays"/> late — one per protocol, so a
    /// treatment several rounds behind produces one nudge, not several.
    /// </summary>
    public static TreatmentRound? EarliestOverdueRound(Treatment t, DateTime now, int overdueDays)
    {
        var threshold = now.AddDays(-overdueDays).Date;
        return t.Rounds
            .Where(r => r.Status == TreatmentRoundStatus.Pending && r.ScheduledDate.Date <= threshold)
            .OrderBy(r => r.ScheduledDate)
            .FirstOrDefault();
    }

    /// <summary>
    /// Same for a feeding programme — and only one that still has a hive on it: a programme with no
    /// active hive has nothing left to do in the field.
    /// </summary>
    public static FeedingEntry? EarliestOverdueFeeding(Diet d, DateTime now, int overdueDays)
    {
        if (d.Status != DietStatus.InProgress || !d.Beehives.Any(db => db.RemovedOn == null)) return null;

        var threshold = now.AddDays(-overdueDays).Date;
        return d.FeedingEntries
            .Where(e => e.Status == FeedingEntryStatus.Pending && e.ScheduledDate.Date <= threshold)
            .OrderBy(e => e.ScheduledDate)
            .FirstOrDefault();
    }
}
