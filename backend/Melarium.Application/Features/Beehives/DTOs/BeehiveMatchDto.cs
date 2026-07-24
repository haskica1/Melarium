namespace Melarium.Application.Features.Beehives.DTOs;

/// <summary>A beehive that matched a scanned/typed number, with its apiary for disambiguation.</summary>
public class BeehiveMatchDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LabelNumber { get; set; }
    public int ApiaryId { get; set; }
    public string? ApiaryName { get; set; }
}
