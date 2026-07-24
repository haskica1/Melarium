using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

/// <summary>
/// A single scheduled feeding event that belongs to a Diet.
/// Entries are auto-generated when a Diet is created or updated.
/// </summary>
public class FeedingEntry : BaseEntity
{
    public DateTime ScheduledDate { get; set; }
    public FeedingEntryStatus Status { get; set; }
    public DateTime? CompletionDate { get; set; }

    // Foreign key
    public int DietId { get; set; }

    // Navigation property
    public Diet Diet { get; set; } = null!;
}
