using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Beehives.DTOs;

public class CreateBeehiveDto
{
    public string Name { get; set; } = string.Empty;
    public BeehiveType Type { get; set; }
    public BeehiveMaterial Material { get; set; }
    public DateTime DateCreated { get; set; }
    public string? Notes { get; set; }
    public string? LabelNumber { get; set; }
    public int ApiaryId { get; set; }
}
