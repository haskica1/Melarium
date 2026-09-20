using Melarium.Domain.Common;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Locks SPEC-25 D4: period membership is decided by the record's date <b>in the app time zone</b>,
/// not by its raw UTC components. The bug this prevents is silent — a harvest recorded late on the
/// last evening of a month lands in the next one, and the monthly report is quietly short.
/// </summary>
public class ReportPeriodTests
{
    // Europe/Sarajevo: UTC+1 winter, UTC+2 summer. Falls back to UTC on a machine without tz data,
    // which would make the offset assertions vacuous — so the tests assert the offset is real first.
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    private static DateTime Utc(int y, int m, int d, int hh = 0, int mm = 0) =>
        new(y, m, d, hh, mm, 0, DateTimeKind.Utc);

    [Fact]
    public void LastEveningOfPeriod_IsInsideIt()
    {
        // 30.09. 23:30 local = 21:30Z. A UTC-only comparison keeps it in September too, but the
        // symmetric case below is the one that breaks.
        var september = (From: new DateOnly(2026, 9, 1), To: new DateOnly(2026, 9, 30));
        Assert.True(ReportPeriod.Contains(september.From, september.To, Utc(2026, 9, 30, 21, 30), Tz));
    }

    [Fact]
    public void InstantThatIsAlreadyNextDayLocally_IsOutsideThePeriod()
    {
        // 30.09. 23:30 UTC is 01:30 on 1 October in Sarajevo — October's row, not September's.
        Assert.Equal(TimeSpan.FromHours(2), Tz.GetUtcOffset(Utc(2026, 9, 30, 23, 30)));

        Assert.False(ReportPeriod.Contains(
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), Utc(2026, 9, 30, 23, 30), Tz));
        Assert.True(ReportPeriod.Contains(
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), Utc(2026, 9, 30, 23, 30), Tz));
    }

    [Fact]
    public void MidnightUtcDates_LandOnTheirOwnCalendarDay()
    {
        // How a <input type="date"> value is actually stored. Sarajevo is east of UTC, so midnight
        // UTC is always the same calendar day locally — both period edges must include it.
        Assert.True(ReportPeriod.Contains(new DateOnly(2026, 3, 1), new DateOnly(2026, 10, 31), Utc(2026, 3, 1), Tz));
        Assert.True(ReportPeriod.Contains(new DateOnly(2026, 3, 1), new DateOnly(2026, 10, 31), Utc(2026, 10, 31), Tz));
        Assert.False(ReportPeriod.Contains(new DateOnly(2026, 3, 1), new DateOnly(2026, 10, 31), Utc(2026, 2, 28), Tz));
        Assert.False(ReportPeriod.Contains(new DateOnly(2026, 3, 1), new DateOnly(2026, 10, 31), Utc(2026, 11, 1), Tz));
    }

    [Fact]
    public void SingleDayPeriod_IsInclusive()
    {
        var day = new DateOnly(2026, 7, 15);
        Assert.True(ReportPeriod.Contains(day, day, Utc(2026, 7, 15, 10), Tz));
        Assert.False(ReportPeriod.Contains(day, day, Utc(2026, 7, 16, 10), Tz));
    }

    [Fact]
    public void UtcBounds_NeverExcludeAnythingContainsWouldAccept()
    {
        // The SQL prefilter is one day wider on each side on purpose: it must not be the thing that
        // decides membership, only the thing that keeps the query bounded.
        var from = new DateOnly(2026, 9, 1);
        var to   = new DateOnly(2026, 9, 30);
        var (fromUtc, toUtc) = ReportPeriod.UtcBounds(from, to);

        for (var d = from.AddDays(-2); d <= to.AddDays(2); d = d.AddDays(1))
            for (var hour = 0; hour < 24; hour++)
            {
                var instant = DateTime.SpecifyKind(d.ToDateTime(new TimeOnly(hour, 0)), DateTimeKind.Utc);
                if (ReportPeriod.Contains(from, to, instant, Tz))
                    Assert.InRange(instant, fromUtc, toUtc);
            }
    }
}
