namespace Melarium.Application.Features.Harvests.DTOs;

public class HarvestEntryDto
{
    public int Id { get; set; }
    public int BeehiveId { get; set; }
    public string? BeehiveName { get; set; }
    public decimal QuantityKg { get; set; }
    public int? FramesExtracted { get; set; }
}
