using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Harvests.Validators;

/// <summary>Bounds shared by the create and update validators (SPEC-02, widened in SPEC-30).</summary>
internal static class HarvestLimits
{
    /// <summary>One milligram — the smallest amount the columns hold, and how bee venom is collected.</summary>
    public const decimal MinKg = 0.000001m;

    /// <summary>Per hive per record.</summary>
    public const decimal MaxEntryKg = 200m;

    /// <summary>One figure for an apiary or a whole organization.</summary>
    public const decimal MaxBulkKg = 100_000m;

    /// <summary>The ceiling of <c>numeric(8,2)</c> — about 1.000 KM/g, above any real venom price.</summary>
    public const decimal MaxPricePerKg = 999_999.99m;

    public const int MaxNotesLength = 500;

    public const int MaxFrames = 200;

    /// <summary>A missing product is honey — what a client older than SPEC-30 means.</summary>
    public static bool IsHoney(HiveProductType? type) => (type ?? HiveProductType.Honey) == HiveProductType.Honey;
}
