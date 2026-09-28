namespace Melarium.Application.Features.Notifications;

/// <summary>
/// How often the organization's AI summary goes out (SPEC-29): weekly, except in winter, when it goes
/// on the first Monday of the month and covers the whole month before it.
/// </summary>
public sealed record SummaryCadence(bool Monthly)
{
    public static readonly SummaryCadence Weekly = new(false);
    public static readonly SummaryCadence FirstMondayOfMonth = new(true);

    /// <summary>Whether a Monday (local date) is a summary day.</summary>
    public bool IsDue(DateOnly localMonday) => !Monthly || localMonday.Day <= 7;

    /// <summary>Start of the period the summary describes.</summary>
    public DateTime PeriodStart(DateTime utcNow) => Monthly ? utcNow.AddMonths(-1) : utcNow.AddDays(-7);

    /// <summary>
    /// The double-run guard. Shorter than the gap between two summaries (7 days, or 28–35), so a
    /// summary is never mistaken for the previous one — only for itself.
    /// </summary>
    public TimeSpan DedupWindow => TimeSpan.FromDays(Monthly ? 20 : 6);

    public string Title => Monthly ? "Mjesečni pregled" : "Sedmični pregled";

    /// <summary>How the digest and the prompt name the period.</summary>
    public string PeriodLabel => Monthly ? "zadnjih mjesec dana" : "zadnjih 7 dana";
}
