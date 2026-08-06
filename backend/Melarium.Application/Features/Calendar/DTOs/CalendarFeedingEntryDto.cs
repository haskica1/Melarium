namespace Melarium.Application.Features.Calendar.DTOs;

/// <summary>
/// One feeding round on the calendar. A round is a single visit to the apiary covering every hive on
/// the programme, so it carries the apiary and a hive count rather than a per-hive identity.
/// </summary>
public class CalendarFeedingEntryDto
{
    public int Id { get; set; }
    public DateTime ScheduledDate { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int DietId { get; set; }
    public string DietName { get; set; } = string.Empty;
    public int ApiaryId { get; set; }
    public string ApiaryName { get; set; } = string.Empty;
    /// <summary>Hives currently on the programme.</summary>
    public int HiveCount { get; set; }
    public string FoodTypeName { get; set; } = string.Empty;
}
