using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Diets.DTOs;

public class DietDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DietReason Reason { get; set; }
    public string ReasonName { get; set; } = string.Empty;
    public string? CustomReason { get; set; }
    public int DurationDays { get; set; }
    public int FrequencyDays { get; set; }
    public FoodType FoodType { get; set; }
    public string FoodTypeName { get; set; } = string.Empty;
    public string? CustomFoodType { get; set; }
    public DietStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string? EarlyCompletionComment { get; set; }
    public int BeehiveId { get; set; }
    public int TotalEntries { get; set; }
    public int CompletedEntries { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}
