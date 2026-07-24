namespace Melarium.Application.Features.Reminders;

/// <summary>
/// Sends each user a single consolidated in-app + email reminder of the day's beekeeping obligations
/// (SPEC-11 Faza A.2). Invoked by the <c>DailyAgendaWorker</c> at 08:00 local; all logic lives here so
/// it is unit-testable without a timer.
/// </summary>
public interface IDailyAgendaService
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
