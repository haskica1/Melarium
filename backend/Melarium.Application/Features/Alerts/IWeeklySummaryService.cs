namespace Melarium.Application.Features.Alerts;

/// <summary>
/// Weekly AI-written digest per organization (SPEC-04 Part B). Runs on Mondays after the daily scan.
/// A failure of the AI step is swallowed — the summary is nice-to-have and must never break the scan.
/// </summary>
public interface IWeeklySummaryService
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
