using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Inspections.DTOs;

public class UpdateInspectionDto
{
    public DateTime Date { get; set; }
    public double? Temperature { get; set; }
    public HoneyLevel HoneyLevel { get; set; }
    public string? BroodStatus { get; set; }
    public string? Notes { get; set; }
    public int BeehiveId { get; set; }
}
