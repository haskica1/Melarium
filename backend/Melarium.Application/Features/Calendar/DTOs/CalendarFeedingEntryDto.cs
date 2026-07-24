namespace Melarium.Application.Features.Calendar.DTOs;

public class CalendarFeedingEntryDto
{
    public int Id { get; set; }
    public DateTime ScheduledDate { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int DietId { get; set; }
    public string DietName { get; set; } = string.Empty;
    public int BeehiveId { get; set; }
    public string BeehiveName { get; set; } = string.Empty;
    public string FoodTypeName { get; set; } = string.Empty;
}
