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
}
