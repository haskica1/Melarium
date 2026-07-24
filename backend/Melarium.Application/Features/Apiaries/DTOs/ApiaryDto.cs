namespace Melarium.Application.Features.Apiaries.DTOs;

/// <summary>Lightweight apiary representation for list views.</summary>
public class ApiaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool HasLocation => Latitude.HasValue && Longitude.HasValue;
    public double? HomeLatitude { get; set; }
    public double? HomeLongitude { get; set; }
    public bool HasHomeLocation => HomeLatitude.HasValue && HomeLongitude.HasValue;
    public int BeehiveCount { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}
