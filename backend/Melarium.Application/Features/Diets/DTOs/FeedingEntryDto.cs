using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Diets.DTOs;

public class FeedingEntryDto
{
    public int Id { get; set; }
    public DateTime ScheduledDate { get; set; }
    public FeedingEntryStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public DateTime? CompletionDate { get; set; }

    /// <summary>Optional note recorded when the round was ticked.</summary>
    public string? Note { get; set; }

    public int DietId { get; set; }
}
