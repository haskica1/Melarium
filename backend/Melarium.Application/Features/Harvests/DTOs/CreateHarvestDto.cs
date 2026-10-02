using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Harvests.DTOs;

/// <summary>
/// Create payload. The quantity comes at exactly one level: per hive (<see cref="Entries"/>), or as one
/// figure (<see cref="BulkKg"/>) for the apiary — or for the whole organization when
/// <see cref="ApiaryId"/> is null (OrganizationAdmin only).
/// </summary>
public class CreateHarvestDto
{
    /// <summary>Null = a record of the whole organization (zajednički).</summary>
    public int? ApiaryId { get; set; }
    public DateTime Date { get; set; }

    /// <summary>Null = honey: what a client older than SPEC-30 means when it sends no product at all.</summary>
    public HiveProductType? ProductType { get; set; }

    /// <summary>Required for honey; ignored for every other product.</summary>
    public HoneyType? HoneyType { get; set; }

    public decimal? PricePerKg { get; set; }
    public decimal? BulkKg { get; set; }
    public string? Notes { get; set; }
    public List<CreateHarvestEntryDto> Entries { get; set; } = [];
}
