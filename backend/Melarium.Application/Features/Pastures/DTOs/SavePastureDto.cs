namespace Melarium.Application.Features.Pastures.DTOs;

/// <summary>Create/update payload — the pasture registry has no separate update shape.</summary>
public class SavePastureDto
{
    public string Name { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Address { get; set; }
    public string? FloraNotes { get; set; }
    public string? Notes { get; set; }
}
