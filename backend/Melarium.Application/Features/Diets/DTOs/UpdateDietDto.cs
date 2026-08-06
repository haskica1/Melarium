using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Diets.DTOs;

/// <summary>
/// Deliberately carries no hive list: <c>DietBeehive</c> rows hold removal history, so replacing the
/// set wholesale on every edit — the way treatment entries are replaced — would silently destroy it.
/// Hives are managed through the two dedicated endpoints instead.
/// </summary>
public class UpdateDietDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DietReason Reason { get; set; }
    public string? CustomReason { get; set; }
    public int DurationDays { get; set; }
    public int FrequencyDays { get; set; }
    public FoodType FoodType { get; set; }
    public string? CustomFoodType { get; set; }

    public decimal? AmountPerHive { get; set; }
    public FeedingAmountUnit? AmountUnit { get; set; }
    public string? AmountNote { get; set; }
}
