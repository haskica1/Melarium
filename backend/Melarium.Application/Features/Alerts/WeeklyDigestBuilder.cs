using System.Globalization;
using System.Text;

namespace Melarium.Application.Features.Alerts;

/// <summary>Deterministic facts gathered for one organization over the past 7 days.</summary>
public record WeeklyDigestInput(
    string OrganizationName,
    int InspectionCount,
    IReadOnlyList<string> InspectionHighlights,
    int FeedingsDone,
    int TodosCreated,
    int TodosCompleted,
    int TodosOverdue,
    decimal HarvestKg,
    IReadOnlyList<string> HoneyTrendLines,
    IReadOnlyList<string> WeatherOutlook)
{
    /// <summary>Whether anything happened worth reporting — orgs with no activity get no summary.</summary>
    public bool HasActivity =>
        InspectionCount > 0 || FeedingsDone > 0 || TodosCreated > 0 || TodosCompleted > 0 || HarvestKg > 0m;
}

/// <summary>
/// Turns the gathered weekly facts into a compact, deterministic Bosnian text block that is fed to the
/// LLM as the *only* source of truth for the weekly summary (SPEC-04 Part B). Pure + unit-testable.
/// </summary>
public static class WeeklyDigestBuilder
{
    public static string Build(WeeklyDigestInput d)
    {
        // Invariant kg formatting keeps the digest deterministic across server locales.
        var kg = d.HarvestKg.ToString("0.#", CultureInfo.InvariantCulture);

        var sb = new StringBuilder();
        sb.AppendLine($"Organizacija: {d.OrganizationName}");
        sb.AppendLine(
            $"Brojke (zadnjih 7 dana): pregledi={d.InspectionCount}, obavljena hranjenja={d.FeedingsDone}, " +
            $"novi zadaci={d.TodosCreated}, završeni zadaci={d.TodosCompleted}, zakašnjeli zadaci={d.TodosOverdue}, " +
            $"prinos meda={kg} kg");

        if (d.InspectionHighlights.Count > 0)
        {
            sb.AppendLine("Zapažanja s pregleda:");
            foreach (var line in d.InspectionHighlights)
                sb.AppendLine($"- {line}");
        }

        if (d.HoneyTrendLines.Count > 0)
        {
            sb.AppendLine("Trend meda po pčelinjaku:");
            foreach (var line in d.HoneyTrendLines)
                sb.AppendLine($"- {line}");
        }

        if (d.WeatherOutlook.Count > 0)
        {
            sb.AppendLine("Vremenska prognoza:");
            foreach (var line in d.WeatherOutlook)
                sb.AppendLine($"- {line}");
        }

        return sb.ToString().TrimEnd();
    }
}
