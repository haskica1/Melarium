using Melarium.Domain.Enums;

namespace Melarium.Domain.Common;

/// <summary>
/// Lightweight read model: one active feeding programme as it applies to one hive. Mirrors
/// <see cref="TreatmentLatestInfo"/>, with one difference — a hive may be on several programmes at
/// once (a stimulative feeding alongside a protein supplement), so consumers always receive a
/// **list** per hive, never a single value.
/// "Active" has a single definition, used by the badge, the advisor context and the calendar alike:
/// a DietBeehive row with RemovedOn == null on a diet whose status is NotStarted or InProgress.
/// </summary>
/// <param name="NextFeedingDate">
/// Earliest pending round from today on; else the earliest pending (overdue) round; else null.
/// </param>
public record DietActiveInfo(
    int BeehiveId,
    int DietId,
    string DietName,
    FoodType FoodType,
    string? CustomFoodType,
    decimal? AmountPerHive,
    FeedingAmountUnit? AmountUnit,
    string? AmountNote,
    DateTime StartDate,
    DateTime? NextFeedingDate,
    int CompletedRounds,
    int TotalRounds);
