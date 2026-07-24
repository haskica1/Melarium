using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Inspections.DTOs;

/// <summary>Inspection data transfer object used for reads.</summary>
public class InspectionDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public double? Temperature { get; set; }
    public HoneyLevel HoneyLevel { get; set; }
    public string HoneyLevelName { get; set; } = string.Empty;
    public string? BroodStatus { get; set; }
    public string? Notes { get; set; }
    public int BeehiveId { get; set; }
    public DateTime CreatedAt { get; set; }
}
