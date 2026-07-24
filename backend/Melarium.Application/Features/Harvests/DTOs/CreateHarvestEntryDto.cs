namespace Melarium.Application.Features.Harvests.DTOs;

public class CreateHarvestEntryDto
{
    public int BeehiveId { get; set; }
    public decimal QuantityKg { get; set; }
    public int? FramesExtracted { get; set; }
}
