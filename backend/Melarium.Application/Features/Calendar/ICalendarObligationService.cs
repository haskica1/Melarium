namespace Melarium.Application.Features.Calendar;

/// <summary>
/// Gathers a user's dated beekeeping obligations over a date window — the single source of truth
/// behind the ICS feed and the daily 08:00 agenda (and, later, native calendar sync). Works off an
/// explicit <see cref="CalendarUserContext"/> so it runs both on the request thread and in the worker.
/// </summary>
public interface ICalendarObligationService
{
    Task<IReadOnlyList<CalendarObligation>> GatherAsync(
        CalendarUserContext ctx, DateOnly from, DateOnly to, CalendarCategories categories);
}
