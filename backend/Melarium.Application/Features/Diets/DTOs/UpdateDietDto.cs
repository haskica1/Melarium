using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Diets.DTOs;

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
}
