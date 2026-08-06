namespace Melarium.Application.Features.Stats.DTOs;

public record StatsDto
{
    public int TotalApiaries { get; init; }
    public int TotalBeehives { get; init; }
    public int TotalInspections { get; init; }
    public int ActiveDiets { get; init; }
    public int PendingTodos { get; init; }

    public IReadOnlyList<NameValueDto> BeehivesByType { get; init; } = [];
    public IReadOnlyList<NameValueDto> BeehivesByMaterial { get; init; } = [];
    public IReadOnlyList<NameValueDto> HoneyLevelDistribution { get; init; } = [];
    public IReadOnlyList<MonthCountDto> InspectionsByMonth { get; init; } = [];
    public IReadOnlyList<MonthTempDto> TemperatureByMonth { get; init; } = [];
    public IReadOnlyList<NameValueDto> DietsByStatus { get; init; } = [];
    public IReadOnlyList<NameValueDto> DietsByFoodType { get; init; } = [];
    public IReadOnlyList<NameValueDto> TopBeehivesByInspections { get; init; } = [];
    public IReadOnlyList<NameValueDto> ApiariesByBeehiveCount { get; init; } = [];
    public IReadOnlyList<PriorityStatsDto> TodosByPriority { get; init; } = [];

    // ── Harvests (SPEC-02) ──────────────────────────────────────────────────────
    /// <summary>Total extracted honey (kg) in the current calendar year.</summary>
    public decimal SeasonTotalKg { get; init; }
    /// <summary>Estimated revenue for the current year: Σ (kg × pricePerKg) over harvests that set a price.</summary>
    public decimal EstimatedRevenue { get; init; }
    public IReadOnlyList<NameDecimalDto> KgByApiary { get; init; } = [];
    public IReadOnlyList<NameDecimalDto> KgByHoneyType { get; init; } = [];
    /// <summary>Top hives by extracted kg in the current year (max 5).</summary>
    public IReadOnlyList<NameDecimalDto> TopHivesByYield { get; init; } = [];
    /// <summary>Total kg per year for the last 3 years (oldest → newest).</summary>
    public IReadOnlyList<NameDecimalDto> YearlyYield { get; init; } = [];

    // ── Pastures (SPEC-10) ──────────────────────────────────────────────────────
    /// <summary>Current-year kg attributed to the pasture the apiary was on at harvest date
    /// ("Matična lokacija" bucket for pre-first-move harvests). Empty when no moves exist.</summary>
    public IReadOnlyList<NameDecimalDto> KgByPasture { get; init; } = [];

    // ── Apiary feeding (SPEC-12 Phase E) ───────────────────────────────────────
    /// <summary>
    /// Money attributed to feeding programmes starting in the current year, BAM only — summing
    /// currencies would be a silent lie, and in practice everything is BAM.
    /// </summary>
    public decimal FeedingCost { get; init; }
}
