namespace Melarium.Application.Features.Pastures.DTOs;

public class PastureDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Address { get; set; }
    public string? FloraNotes { get; set; }
    public string? Notes { get; set; }

    /// <summary>How many apiaries currently sit on this pasture.</summary>
    public int ApiariesOnPasture { get; set; }

    public DateTime CreatedAt { get; set; }
}
