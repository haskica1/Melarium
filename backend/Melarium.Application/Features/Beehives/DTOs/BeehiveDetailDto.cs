using Melarium.Application.Features.Inspections.DTOs;

namespace Melarium.Application.Features.Beehives.DTOs;

/// <summary>Full beehive representation including its inspections and QR code.</summary>
public class BeehiveDetailDto : BeehiveDto
{
    // The QR PNG lives only on the detail DTO — on list DTOs it would add kilobytes per hive.
    public string? QrCodeBase64 { get; set; }
    public IEnumerable<InspectionDto> Inspections { get; set; } = new List<InspectionDto>();
}
