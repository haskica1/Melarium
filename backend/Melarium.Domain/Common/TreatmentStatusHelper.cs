using Melarium.Domain.Enums;

namespace Melarium.Domain.Common;

/// <summary>
/// Pure derivation of a treatment's withdrawal-end date (karenca) and status — the single source of
/// truth reused by the DTO mapping, hive badge, alerts, and advisor context (SPEC-08).
/// </summary>
public static class TreatmentStatusHelper
{
    /// <summary>End of the withdrawal window: <c>(endDate ?? startDate) + withdrawalDays</c>.</summary>
    public static DateTime KarencaUntil(DateTime startDate, DateTime? endDate, int withdrawalDays) =>
        (endDate ?? startDate).AddDays(withdrawalDays);

    /// <summary>
    /// Status as of <paramref name="asOf"/>: no end date → InProgress; a zero withdrawal → Completed
    /// once ended (no karenca phase); otherwise Karenca until <see cref="KarencaUntil"/>, then Completed.
    /// </summary>
    public static TreatmentStatus Status(DateTime startDate, DateTime? endDate, int withdrawalDays, DateTime asOf)
    {
        if (endDate is null) return TreatmentStatus.InProgress;
        if (withdrawalDays <= 0) return TreatmentStatus.Completed; // no karenca phase

        var karencaUntil = endDate.Value.AddDays(withdrawalDays);
        return asOf.Date <= karencaUntil.Date ? TreatmentStatus.Karenca : TreatmentStatus.Completed;
    }
}
