using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

/// <summary>
/// Represents a feeding programme (diet) for a specific beehive.
/// A diet defines a structured feeding schedule with automatic entry generation.
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

    public DietStatus Status { get; set; }

    /// <summary>Required comment when a diet is stopped early or completed before all entries.</summary>
    public string? EarlyCompletionComment { get; set; }

    public int? CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    // Foreign key
    public int BeehiveId { get; set; }

    // Navigation properties
    public Beehive Beehive { get; set; } = null!;
    public ICollection<FeedingEntry> FeedingEntries { get; set; } = new List<FeedingEntry>();
}
