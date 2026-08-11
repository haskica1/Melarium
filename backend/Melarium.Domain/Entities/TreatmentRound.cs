using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

/// <summary>
/// A single scheduled application round for the whole group of hives on a Treatment.
/// Rounds are auto-generated when a Treatment is created or updated (<see cref="Treatment.TotalRounds"/>
/// / <see cref="Treatment.IntervalDays"/>), the same shape as <see cref="FeedingEntry"/> for a Diet.
/// </summary>
public class TreatmentRound : BaseEntity
{
    public DateTime ScheduledDate { get; set; }
    public TreatmentRoundStatus Status { get; set; }
    public DateTime? CompletionDate { get; set; }

    /// <summary>Optional free-text note recorded when the round is ticked.</summary>
    public string? Note { get; set; }

    // Foreign key
    public int TreatmentId { get; set; }

    // Navigation property
    public Treatment Treatment { get; set; } = null!;
}
