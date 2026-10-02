using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Harvests.DTOs;

/// <summary>Lightweight harvest representation used in list views (totals derived).</summary>
public class HarvestDto
{
    public int Id { get; set; }

    /// <summary>Null for a record of the whole organization (zajednički), SPEC-30.</summary>
    public int? ApiaryId { get; set; }
    public string? ApiaryName { get; set; }
    public DateTime Date { get; set; }

    /// <summary>What was collected (SPEC-30). Honey for every record older than that.</summary>
    public HiveProductType ProductType { get; set; }
    public string ProductTypeName { get; set; } = string.Empty;

    /// <summary>Honey only; null for every other product.</summary>
    public HoneyType? HoneyType { get; set; }

    /// <summary>Empty for every product but honey — kept non-null for clients that predate SPEC-30.</summary>
    public string HoneyTypeName { get; set; } = string.Empty;

    /// <summary>KM per kg, even for products the UI prices per gram.</summary>
    public decimal? PricePerKg { get; set; }

    /// <summary>The quantity when it was not split per hive (apiary or organization level); null otherwise.</summary>
    public decimal? BulkKg { get; set; }

    public string? Notes { get; set; }

    /// <summary><see cref="BulkKg"/>, or the sum of all entry quantities (kg).</summary>
    public decimal TotalKg { get; set; }
    public int EntryCount { get; set; }

    /// <summary>TotalKg × PricePerKg when a price is set; otherwise null.</summary>
    public decimal? EstimatedRevenue { get; set; }

    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}
