namespace Melarium.Application.Features.Calendar.DTOs;

public class CalendarEventsDto
{
    public List<CalendarTodoDto> Todos { get; set; } = new();
    public List<CalendarFeedingEntryDto> FeedingEntries { get; set; } = new();
}
