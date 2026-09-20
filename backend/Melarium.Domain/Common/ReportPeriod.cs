namespace Melarium.Domain.Common;

/// <summary>
/// Period membership for the season report (SPEC-25 D4). A record belongs to the period when its
/// date, <b>converted to the application time zone</b>, falls inside <c>[from, to]</c> inclusive.
///
/// <para>
/// The zone matters: everything is stored UTC (ADR-037), so a plain <c>date.Year == year</c> check —
/// which is what <c>StatsService</c> does — puts a harvest recorded at 23:30 local on 30 September
/// into October. The user picked calendar dates, not instants, so the calendar is what decides.
/// </para>
///
/// <para>
/// <see cref="UtcBounds"/> is a <b>prefilter</b> for the database, deliberately one day wider on each
/// side than the period: it must never exclude a row that <see cref="Contains"/> would accept, no
/// matter which offset the zone had on that date. The exact answer is always <see cref="Contains"/>.
/// </para>
/// </summary>
public static class ReportPeriod
{
    public static (DateTime FromUtc, DateTime ToUtc) UtcBounds(DateOnly from, DateOnly to) =>
    (
        DateTime.SpecifyKind(from.AddDays(-1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
        DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc)
    );

    public static bool Contains(DateOnly from, DateOnly to, DateTime instant, TimeZoneInfo tz)
    {
        var local = LocalDateOf(instant, tz);
        return local >= from && local <= to;
    }

    /// <summary>The calendar date an instant falls on in the application time zone.</summary>
    public static DateOnly LocalDateOf(DateTime instant, TimeZoneInfo tz) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(instant, DateTimeKind.Utc), tz));
}
