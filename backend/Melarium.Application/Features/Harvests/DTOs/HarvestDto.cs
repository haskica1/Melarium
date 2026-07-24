using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Harvests.DTOs;

/// <summary>Lightweight harvest representation used in list views (totals precomputed).</summary>
public class HarvestDto
{
    public int Id { get; set; }
    public int ApiaryId { get; set; }
    public string? ApiaryName { get; set; }
    public DateTime Date { get; set; }
    public HoneyType HoneyType { get; set; }
    public string HoneyTypeName { get; set; } = string.Empty;
    public decimal? PricePerKg { get; set; }
    public string? Notes { get; set; }

    /// <summary>Sum of all entry quantities (kg).</summary>
    public decimal TotalKg { get; set; }
    public int EntryCount { get; set; }

    /// <summary>TotalKg × PricePerKg when a price is set; otherwise null.</summary>
    public decimal? EstimatedRevenue { get; set; }

    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}
