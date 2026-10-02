namespace Melarium.Application.Common.Security;

/// <summary>Plan-gated features (SPEC-09). Availability is decided by <see cref="IPlanGuard"/>.</summary>
public enum PlanFeature
{
    /// <summary>Voice input for inspections (Standard+).</summary>
    VoiceInput = 1,

    /// <summary>Weekly AI summary (Standard+); the worker skips organizations without it.</summary>
    WeeklySummary = 2,

    /// <summary>Pasture registry + apiary moves (Standard+).</summary>
    Pastures = 3,

    /// <summary>AI frame photo analysis, SPEC-05 (Pro+).</summary>
    PhotoAnalysis = 4,

    // 5 is Achievements (SPEC-27), which is still in a stash — kept free so the two never collide.

    /// <summary>
    /// Recording bee products other than honey, SPEC-30 (Standard+). Only writing is gated: a Free
    /// organization still reads what it recorded before, so a report for a past season stays whole.
    /// </summary>
    HiveProducts = 6,
}
