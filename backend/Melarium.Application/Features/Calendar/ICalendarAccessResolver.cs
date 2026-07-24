namespace Melarium.Application.Features.Calendar;

/// <summary>
/// Resolves which apiaries/hives a calendar user may see, from an explicit <see cref="CalendarUserContext"/>
/// (not <c>ICurrentUser</c>) so the same logic serves the authenticated calendar, the anonymous
/// token-based ICS feed, and the background daily agenda.
/// </summary>
public interface ICalendarAccessResolver
{
    Task<CalendarScope> ResolveAsync(CalendarUserContext ctx);
}
