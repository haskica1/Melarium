using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Diets.DTOs;

public class CreateDietDto
{
    public int ApiaryId { get; set; }
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

    /// <summary>Hives of <see cref="ApiaryId"/> the programme covers. At least one; never trusted blindly.</summary>
    public List<int> BeehiveIds { get; set; } = [];
}
