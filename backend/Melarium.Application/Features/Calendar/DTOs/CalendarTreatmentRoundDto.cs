namespace Melarium.Application.Features.Calendar.DTOs;

/// <summary>
/// One treatment application round on the calendar. A round is a single visit to the apiary covering
/// every treated hive, so it carries the apiary and a hive count rather than a per-hive identity —
/// the same shape as <see cref="CalendarFeedingEntryDto"/>.
/// </summary>
public class CalendarTreatmentRoundDto
{
    public int Id { get; set; }
    public DateTime ScheduledDate { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int TreatmentId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int ApiaryId { get; set; }
    public string ApiaryName { get; set; } = string.Empty;
    /// <summary>Hives on this treatment.</summary>
    public int HiveCount { get; set; }
    public string PurposeName { get; set; } = string.Empty;
}
