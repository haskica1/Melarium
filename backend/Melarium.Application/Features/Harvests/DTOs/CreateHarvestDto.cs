using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Harvests.DTOs;

public class CreateHarvestDto
{
    public int ApiaryId { get; set; }
    public DateTime Date { get; set; }
    public HoneyType HoneyType { get; set; }
    public decimal? PricePerKg { get; set; }
    public string? Notes { get; set; }
    public List<CreateHarvestEntryDto> Entries { get; set; } = [];
}
