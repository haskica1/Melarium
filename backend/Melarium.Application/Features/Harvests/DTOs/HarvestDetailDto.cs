namespace Melarium.Application.Features.Harvests.DTOs;

/// <summary>Full harvest representation including its per-hive entries.</summary>
public class HarvestDetailDto : HarvestDto
{
    public List<HarvestEntryDto> Entries { get; set; } = [];
}
