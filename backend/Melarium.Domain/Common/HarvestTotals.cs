using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Common;

/// <summary>
/// The quantity and revenue of a <see cref="Harvest"/> — derived, never stored (SPEC-30), the way
/// <see cref="TreatmentStatusHelper"/> derives karenca. The single definition behind the lists, the hive
/// card, the stats, the dashboard and the season report, so none of them can count a record differently.
/// </summary>
public static class HarvestTotals
{
    /// <summary>
    /// <see cref="Harvest.BulkKg"/> when the quantity was recorded as one figure (for the apiary or the
    /// whole organization), otherwise the sum of the per-hive lines. Zero once every hive of a split
    /// record has been deleted — the lines cascade with their hives.
    /// </summary>
    public static decimal TotalKg(Harvest h) =>
        h.BulkKg ?? h.Entries.Sum(e => e.QuantityKg);

    /// <summary>Kg × price, or null without a price — callers keep that kg apart as "unpriced".</summary>
    public static decimal? EstimatedRevenue(Harvest h) =>
        h.PricePerKg is decimal price ? TotalKg(h) * price : null;

    /// <summary>The in-memory twin of the repository's kind filter.</summary>
    public static bool Matches(HarvestKind kind, HiveProductType type) => kind switch
    {
        HarvestKind.Honey         => type == HiveProductType.Honey,
        HarvestKind.OtherProducts => type != HiveProductType.Honey,
        _                         => true,
    };
}
