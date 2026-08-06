using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Diets.DTOs;

/// <summary>List-view feeding programme. Apiary-scoped since SPEC-12 — a hive reaches it through
/// the hive links, so there is no single <c>BeehiveId</c> any more.</summary>
public class DietDto
{
    public int Id { get; set; }
    public int ApiaryId { get; set; }
    public string? ApiaryName { get; set; }

    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DietReason Reason { get; set; }
    public string ReasonName { get; set; } = string.Empty;
    public string? CustomReason { get; set; }
    public int DurationDays { get; set; }
    public int FrequencyDays { get; set; }
    public FoodType FoodType { get; set; }
    public string FoodTypeName { get; set; } = string.Empty;
    public string? CustomFoodType { get; set; }

    public decimal? AmountPerHive { get; set; }
    public FeedingAmountUnit? AmountUnit { get; set; }
    /// <summary>Short unit label ("L", "kg", …); null when no amount was recorded.</summary>
    public string? AmountUnitName { get; set; }
    public string? AmountNote { get; set; }

    public DietStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string? EarlyCompletionComment { get; set; }

    /// <summary>Hives currently on the programme. Removed links are history and are never counted.</summary>
    public int HiveCount { get; set; }

    public int TotalEntries { get; set; }
    public int CompletedEntries { get; set; }

    /// <summary>Earliest pending round from today on; else the earliest overdue one; else null.</summary>
    public DateTime? NextFeedingDate { get; set; }

    // ── Feeding cost (SPEC-12 Phase E) ─────────────────────────────────────────
    // Potrošnja (planning, from the programme) vs. Trošak (accounting, from attributed expenses) —
    // deliberately never subtracted from or converted into each other.

    /// <summary>Exact planned consumption over ALL rounds — null when AmountPerHive is unset.</summary>
    public decimal? PlannedAmount { get; set; }
    /// <summary>Same fold, but only over completed rounds — how much has actually been used so far.</summary>
    public decimal? ConsumedAmount { get; set; }

    /// <summary>
    /// Attributed ExpenseItem totals grouped by currency. Null both when nothing has been attributed
    /// yet and when the caller is a Beekeeper (no Expenses access) — server-side omission, not a
    /// hidden-but-transmitted number.
    /// </summary>
    public List<CostTotalDto>? CostTotals { get; set; }
    /// <summary>Total ÷ active hive count — only when there is exactly one currency and hiveCount &gt; 0.</summary>
    public decimal? CostPerHive { get; set; }

    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}
