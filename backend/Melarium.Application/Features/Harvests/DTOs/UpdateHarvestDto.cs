using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Harvests.DTOs;

/// <summary>
/// Update payload. The apiary is immutable after creation (entries belong to that apiary's hives, and a
/// record of the whole organization stays one), so it is not part of this DTO. Switching between a
/// per-hive split and one figure is allowed; the entry set is replaced either way.
/// </summary>
public class UpdateHarvestDto
{
    public DateTime Date { get; set; }

    /// <summary>Null = keep the record's product — what a client older than SPEC-30 sends.</summary>
    public HiveProductType? ProductType { get; set; }

    /// <summary>Required for honey; ignored for every other product.</summary>
    public HoneyType? HoneyType { get; set; }

    public decimal? PricePerKg { get; set; }
    public decimal? BulkKg { get; set; }
    public string? Notes { get; set; }
    public List<CreateHarvestEntryDto> Entries { get; set; } = [];
}
