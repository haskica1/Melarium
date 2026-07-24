using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Beehives.DTOs;

/// <summary>Lightweight beehive representation for list/summary views.</summary>
public class BeehiveDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public BeehiveType Type { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public BeehiveMaterial Material { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; }
    public string? Notes { get; set; }
    public string? LabelNumber { get; set; }
    public int ApiaryId { get; set; }
    public int InspectionCount { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? UniqueId { get; set; }
}
