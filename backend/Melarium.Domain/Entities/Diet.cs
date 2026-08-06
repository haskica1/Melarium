using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

/// <summary>
/// Represents a feeding programme (diet) for an apiary. The programme is defined once and covers a
/// chosen set of that apiary's hives via <see cref="Beehives"/> — the same shape as a treatment.
/// A <see cref="FeedingEntry"/> is one feeding round for the whole group, not per hive: feeding is
/// a single visit with one canister, so the schedule lives at programme level.
/// </summary>
public class Diet : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DietReason Reason { get; set; }

    /// <summary>Free-text reason when Reason == DietReason.Custom.</summary>
    public string? CustomReason { get; set; }

    /// <summary>Total length of the feeding programme in days.</summary>
    public int DurationDays { get; set; }

    /// <summary>How often (in days) a feeding event occurs, e.g. 2 = every 2 days.</summary>
    public int FrequencyDays { get; set; }

    public FoodType FoodType { get; set; }

    /// <summary>Free-text food description when FoodType == FoodType.Custom.</summary>
    public string? CustomFoodType { get; set; }

    /// <summary>How much each hive gets per round. Optional; kept numeric so consumption is computable.</summary>
    public decimal? AmountPerHive { get; set; }

    /// <summary>Required as soon as <see cref="AmountPerHive"/> is set.</summary>
    public FeedingAmountUnit? AmountUnit { get; set; }

    /// <summary>
    /// What the number cannot carry: "1:1", "2:1", "pola pogače". Valid on its own, without an amount —
    /// 1 L of 1:1 syrup and 1 L of 2:1 are the same litre and a completely different intervention.
    /// </summary>
    public string? AmountNote { get; set; }

    public DietStatus Status { get; set; }

    /// <summary>Required comment when a diet is stopped early or completed before all entries.</summary>
    public string? EarlyCompletionComment { get; set; }

    public int? CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    // Foreign key
    public int ApiaryId { get; set; }

    // Navigation properties
    public Apiary Apiary { get; set; } = null!;
    public ICollection<DietBeehive> Beehives { get; set; } = new List<DietBeehive>();
    public ICollection<FeedingEntry> FeedingEntries { get; set; } = new List<FeedingEntry>();
}
