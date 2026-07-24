namespace Melarium.Application.Features.Alerts;

/// <summary>
/// Runs the proactive rule-based alert scan (SPEC-04, Part A). Kept separate from the hosting
/// background worker so the rule logic is unit-testable without a timer.
/// </summary>
public interface IAlertRuleService
{
    /// <summary>Evaluates every enabled rule across all apiaries and dispatches deduplicated notifications.</summary>
    Task RunDailyScanAsync(CancellationToken cancellationToken = default);
}
