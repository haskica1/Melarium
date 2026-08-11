using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Treatments.DTOs;

public class TreatmentRoundDto
{
    public int Id { get; set; }
    public DateTime ScheduledDate { get; set; }
    public TreatmentRoundStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public DateTime? CompletionDate { get; set; }

    /// <summary>Optional note recorded when the round was ticked.</summary>
    public string? Note { get; set; }

    public int TreatmentId { get; set; }
}
