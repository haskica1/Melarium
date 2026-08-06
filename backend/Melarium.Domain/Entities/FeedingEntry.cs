using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

/// <summary>
/// A single scheduled feeding round for the whole group of hives on a Diet.
/// Entries are auto-generated when a Diet is created or updated.
/// </summary>
public class FeedingEntry : BaseEntity
{
    public DateTime ScheduledDate { get; set; }
    public FeedingEntryStatus Status { get; set; }
    public DateTime? CompletionDate { get; set; }

    /// <summary>
    /// Optional free-text note recorded when the round is ticked — the escape hatch for the rare
    /// exception that per-round hive tracking would otherwise be needed for
    /// ("košnice 7 i 12 preskočene — slabe").
    /// </summary>
    public string? Note { get; set; }

    // Foreign key
    public int DietId { get; set; }

    // Navigation property
    public Diet Diet { get; set; } = null!;
}
