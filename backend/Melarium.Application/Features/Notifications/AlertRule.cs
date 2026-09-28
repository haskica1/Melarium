using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Notifications;

/// <summary>What the notification policy says about one alert type in one season phase (SPEC-29).</summary>
/// <param name="Enabled">Whether the rule runs at all — the season and the <c>Alerts:{Rule}:Enabled</c> switch together.</param>
/// <param name="DedupWindow">How far back an existing (user, type, entity) notification suppresses a repeat.</param>
/// <param name="OncePerPhase">Suppress repeats from the first day of the phase instead of for a window.</param>
/// <param name="ThresholdDays">Days without an inspection before <c>InspectionOverdue</c> fires.</param>
/// <param name="BelowCelsius"><c>FrostWarning</c> fires when the forecast minimum is strictly below this.</param>
public sealed record AlertRule(
    bool Enabled,
    NotificationPriority Priority,
    TimeSpan DedupWindow,
    bool OncePerPhase = false,
    int? ThresholdDays = null,
    double? BelowCelsius = null)
{
    public static readonly AlertRule Off = new(false, NotificationPriority.Normal, TimeSpan.Zero);
}
