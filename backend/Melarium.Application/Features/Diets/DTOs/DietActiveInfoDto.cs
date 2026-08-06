using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Diets.DTOs;

/// <summary>
/// One (hive × active programme) pair. Returned as a flat array so a whole apiary's hive badges cost
/// one request; the frontend groups by <see cref="BeehiveId"/>. A hive appears more than once when it
/// is on overlapping programmes.
/// </summary>
public class DietActiveInfoDto
{
    public int BeehiveId { get; set; }
    public int DietId { get; set; }
    public string DietName { get; set; } = string.Empty;

    public FoodType FoodType { get; set; }
    public string FoodTypeName { get; set; } = string.Empty;
    public string? CustomFoodType { get; set; }

    public decimal? AmountPerHive { get; set; }
    public FeedingAmountUnit? AmountUnit { get; set; }
    public string? AmountUnitName { get; set; }
    public string? AmountNote { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? NextFeedingDate { get; set; }

    public int CompletedRounds { get; set; }
    public int TotalRounds { get; set; }
}
