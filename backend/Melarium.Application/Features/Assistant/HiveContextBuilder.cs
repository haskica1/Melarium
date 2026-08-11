using System.Globalization;
using System.Text;
using Melarium.Application.Common.Localization;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Assistant;

/// <summary>
/// Builds the compact plain-text hive-context block appended to the assistant's system prompt when a
/// beehive is in scope (SPEC-18; originally SPEC-01's <c>AdvisorContextBuilder</c>). Pure and
/// unit-testable: it takes already-fetched data and never does I/O. Sections are additive — a hive
/// with no queen/diet/harvest simply omits those lines.
/// </summary>
public static class HiveContextBuilder
{
    public static string Build(
        Beehive hive,
        string apiaryName,
        IReadOnlyList<Inspection> recentInspections,      // newest first, already capped to 5
        IReadOnlyList<DietActiveInfo> activeDiets,        // a hive may be on several at once
        IReadOnlyList<Todo> openTodos,                    // already capped to 5
        Queen? activeQueen,
        decimal? seasonYieldKg,
        TreatmentLatestInfo? latestTreatment,
        string? pastureLine,
        string? weatherLine)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PODACI O KOŠNICI (koristi ih u odgovoru; ništa ne izmišljaj izvan ovih podataka):");
        sb.AppendLine($"- Košnica: {hive.Name} ({BsLabels.Label(hive.Type)}, {BsLabels.Label(hive.Material)}); pčelinjak: {apiaryName}");

        if (!string.IsNullOrWhiteSpace(pastureLine))
            sb.AppendLine($"- Pašnjak: {pastureLine}");

        if (activeQueen is not null)
        {
            var season = DateTime.UtcNow.Year - activeQueen.Year + 1;
            sb.AppendLine($"- Matica: godina {activeQueen.Year} ({season}. sezona), status {BsLabels.Label(activeQueen.Status)}, porijeklo {BsLabels.Label(activeQueen.Origin)}");
        }

        if (seasonYieldKg is decimal kg && kg > 0)
            sb.AppendLine($"- Prinos meda ove sezone: {kg.ToString("0.#", CultureInfo.InvariantCulture)} kg");

        if (recentInspections.Count == 0)
        {
            sb.AppendLine("- Pregledi: nema zabilježenih pregleda.");
        }
        else
        {
            sb.AppendLine($"- Zadnji pregledi ({recentInspections.Count}):");
            foreach (var i in recentInspections)
            {
                var brood = string.IsNullOrWhiteSpace(i.BroodStatus) ? "" : $"; leglo: {Truncate(i.BroodStatus!, 200)}";
                var notes = string.IsNullOrWhiteSpace(i.Notes) ? "" : $"; napomena: {Truncate(i.Notes!, 200)}";
                sb.AppendLine($"  • {i.Date:dd.MM.yyyy}: med {BsLabels.Label(i.HoneyLevel)}{brood}{notes}");
            }
        }

        foreach (var d in activeDiets)
        {
            var food   = d.FoodType == FoodType.Custom ? (d.CustomFoodType ?? "Vlastito") : BsLabels.Label(d.FoodType);
            var amount = d.AmountPerHive is decimal a && d.AmountUnit is FeedingAmountUnit u
                ? $", {a.ToString("0.##", CultureInfo.InvariantCulture)} {BsLabels.Label(u)}"
                : "";
            var note   = string.IsNullOrWhiteSpace(d.AmountNote) ? "" : $" ({d.AmountNote})";
            var next   = d.NextFeedingDate.HasValue ? $", sljedeće {d.NextFeedingDate.Value:dd.MM.yyyy}" : "";
            sb.AppendLine($"- Aktivna prehrana: {food}{amount}{note} ({d.CompletedRounds}/{d.TotalRounds} rundi{next})");
        }

        if (latestTreatment is not null)
        {
            var status = TreatmentStatusHelper.Status(
                latestTreatment.StartDate, latestTreatment.EndDate, latestTreatment.WithdrawalDays, DateTime.UtcNow);
            var statusText = status == TreatmentStatus.Karenca
                ? $"Karenca do {TreatmentStatusHelper.KarencaUntil(latestTreatment.StartDate, latestTreatment.EndDate, latestTreatment.WithdrawalDays):dd.MM.yyyy}"
                : BsLabels.Label(status);
            sb.AppendLine($"- Zadnji tretman: {latestTreatment.ProductName} ({BsLabels.Label(latestTreatment.ActiveSubstance)}), {latestTreatment.StartDate:dd.MM.yyyy}, status: {statusText}");
        }

        if (openTodos.Count > 0)
        {
            sb.AppendLine("- Otvoreni zadaci:");
            foreach (var t in openTodos)
            {
                var due = t.DueDate.HasValue ? $", rok {t.DueDate.Value:dd.MM.yyyy}" : "";
                sb.AppendLine($"  • {t.Title} (prioritet {BsLabels.Label(t.Priority)}{due})");
            }
        }

        if (!string.IsNullOrWhiteSpace(weatherLine))
            sb.AppendLine($"- Vrijeme na pčelinjaku: {weatherLine}");

        return sb.ToString().TrimEnd();
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max].TrimEnd() + "…";
}
