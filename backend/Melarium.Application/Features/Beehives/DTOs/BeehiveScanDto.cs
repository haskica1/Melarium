namespace Melarium.Application.Features.Beehives.DTOs;

/// <summary>Minimal public DTO returned by the unauthenticated QR scan lookup.</summary>
public class BeehiveScanDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ApiaryId { get; set; }
}
