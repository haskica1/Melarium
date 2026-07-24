namespace Melarium.Application.Features.Beehives.DTOs;

/// <summary>QR-code payload for label printing — fetched on demand, never with list views.</summary>
public class BeehiveQrDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? UniqueId { get; set; }
    public string? QrCodeBase64 { get; set; }
}
