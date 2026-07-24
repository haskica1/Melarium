using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Harvests.DTOs;

/// <summary>
/// Update payload. The apiary is immutable after creation (entries belong to that apiary's hives),
/// so it is not part of this DTO — only the harvest details and its entry set can change.
/// </summary>
public class UpdateHarvestDto
{
    public DateTime Date { get; set; }
    public HoneyType HoneyType { get; set; }
    public decimal? PricePerKg { get; set; }
    public string? Notes { get; set; }
    public List<CreateHarvestEntryDto> Entries { get; set; } = [];
}
