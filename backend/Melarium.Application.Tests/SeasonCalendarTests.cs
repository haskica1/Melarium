using Melarium.Application.Common.Seasons;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// The beekeeping year (SPEC-29): default boundaries for continental Bosnia, the altitude shift that
/// squeezes the season instead of sliding it, and boundaries decided on the local calendar, not UTC.
/// </summary>
public class SeasonCalendarTests
{
    private readonly SeasonCalendar _calendar = TestSeasons.Calendar();

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    [Theory]
    [InlineData(1, 1, SeasonPhase.Winter)]
    [InlineData(2, 15, SeasonPhase.Winter)]
    [InlineData(2, 16, SeasonPhase.SpringBuildUp)]
    [InlineData(4, 15, SeasonPhase.SpringBuildUp)]
    [InlineData(4, 16, SeasonPhase.MainSeason)]
    [InlineData(7, 31, SeasonPhase.MainSeason)]
    [InlineData(8, 1, SeasonPhase.LateSummer)]
    [InlineData(9, 30, SeasonPhase.LateSummer)]
    [InlineData(10, 1, SeasonPhase.Wintering)]
    [InlineData(11, 14, SeasonPhase.Wintering)]
    [InlineData(11, 15, SeasonPhase.Winter)]
    [InlineData(12, 31, SeasonPhase.Winter)]
    public void DefaultBoundaries_MatchTheSpec(int month, int day, SeasonPhase expected)
    {
        Assert.Equal(expected, _calendar.For(D(2026, month, day), shiftDays: 0).Phase);
    }

    [Fact]
    public void Winter_SpansTheNewYear_WithBoundsOnBothSides()
    {
        var january = _calendar.For(D(2027, 1, 10), 0);
        Assert.Equal(D(2026, 11, 15), january.Start);
        Assert.Equal(D(2027, 2, 15), january.End);
        Assert.Equal(SeasonPhase.SpringBuildUp, january.NextPhase);
        Assert.Equal(D(2027, 2, 16), january.NextStart);

        var december = _calendar.For(D(2026, 12, 10), 0);
        Assert.Equal(D(2026, 11, 15), december.Start);
        Assert.Equal(D(2027, 2, 16), december.NextStart);
    }

    [Fact]
    public void NextPhase_IsReportedWithItsStartDate()
    {
        var september = _calendar.For(D(2026, 9, 26), 0);

        Assert.Equal(SeasonPhase.LateSummer, september.Phase);
        Assert.Equal(D(2026, 8, 1), september.Start);
        Assert.Equal(D(2026, 9, 30), september.End);
        Assert.Equal(SeasonPhase.Wintering, september.NextPhase);
        Assert.Equal(D(2026, 10, 1), september.NextStart);
    }

    // ── The organization's shift ─────────────────────────────────────────────────

    [Fact]
    public void Shift_MovesSpringLater_AndWinterEarlier_AndLeavesAugustAlone()
    {
        var cycle = _calendar.CycleFor(D(2026, 6, 1), shiftDays: 21);

        Assert.Equal(D(2025, 10, 25), cycle[0].Start);                          // winter from 15.11. − 21
        Assert.Equal(D(2026, 3, 9), cycle[1].Start);                            // spring 16.02. + 21
        Assert.Equal(D(2026, 5, 7), cycle[2].Start);                            // main season 16.04. + 21
        Assert.Equal(D(2026, 8, 1), cycle[3].Start);                            // late summer fixed
        Assert.Equal(D(2026, 9, 10), cycle[4].Start);                           // wintering 01.10. − 21
        Assert.Equal(D(2026, 10, 24), cycle[4].End);
    }

    [Fact]
    public void Shift_MovesAPhaseBoundary_ForTheSameDay()
    {
        // 1 March: spring in the valley, still winter in the mountains.
        Assert.Equal(SeasonPhase.SpringBuildUp, _calendar.For(D(2026, 3, 1), 0).Phase);
        Assert.Equal(SeasonPhase.Winter, _calendar.For(D(2026, 3, 1), 21).Phase);

        // 30 October: still wintering in the valley, winter already in the mountains.
        Assert.Equal(SeasonPhase.Wintering, _calendar.For(D(2026, 10, 30), 0).Phase);
        Assert.Equal(SeasonPhase.Winter, _calendar.For(D(2026, 10, 30), 21).Phase);
    }

    [Fact]
    public void NegativeShift_GivesTheLowlandsALongerSeason()
    {
        var cycle = _calendar.CycleFor(D(2026, 6, 1), shiftDays: -14);

        Assert.Equal(D(2026, 2, 2), cycle[1].Start);     // spring two weeks earlier
        Assert.Equal(D(2026, 10, 15), cycle[4].Start);   // wintering two weeks later
        Assert.Equal(D(2026, 11, 28), cycle[4].End);     // winter from 29.11.
    }

    [Fact]
    public void Shift_IsClampedToTheAllowedRange()
    {
        Assert.Equal(
            _calendar.For(D(2026, 3, 20), SeasonCalendar.MaxShiftDays),
            _calendar.For(D(2026, 3, 20), 100));
    }

    [Fact]
    public void Cycle_ForDecember_BelongsToNextYear()
    {
        var cycle = _calendar.CycleFor(D(2026, 12, 20), 0);

        Assert.Equal(SeasonPhase.Winter, cycle[0].Phase);
        Assert.Equal(D(2026, 11, 15), cycle[0].Start);
        Assert.Equal(D(2027, 2, 16), cycle[1].Start);
        Assert.Equal(5, cycle.Count);
    }

    // ── Local time ───────────────────────────────────────────────────────────────

    [Fact]
    public void Boundary_IsDecidedInTheLocalZone_NotInUtc()
    {
        // 23:30 UTC on 14 November is 00:30 on the 15th in Sarajevo (CET, UTC+1): winter has begun.
        var justAfter = _calendar.LocalDate(new DateTime(2026, 11, 14, 23, 30, 0, DateTimeKind.Utc));
        Assert.Equal(D(2026, 11, 15), justAfter);
        Assert.Equal(SeasonPhase.Winter, _calendar.For(justAfter, 0).Phase);

        // An hour earlier it is still the 14th locally.
        var justBefore = _calendar.LocalDate(new DateTime(2026, 11, 14, 22, 30, 0, DateTimeKind.Utc));
        Assert.Equal(SeasonPhase.Wintering, _calendar.For(justBefore, 0).Phase);
    }

    [Fact]
    public void Boundary_IsLocal_InSummerTimeToo()
    {
        // 22:30 UTC on 31 July is 00:30 on 1 August in CEST (UTC+2).
        var local = _calendar.LocalDate(new DateTime(2026, 7, 31, 22, 30, 0, DateTimeKind.Utc));
        Assert.Equal(SeasonPhase.LateSummer, _calendar.For(local, 0).Phase);
    }

    [Fact]
    public void StartOfDayUtc_IsLocalMidnight()
    {
        // CET after the last Sunday of October, CEST in August.
        Assert.Equal(new DateTime(2026, 10, 31, 23, 0, 0, DateTimeKind.Utc), _calendar.StartOfDayUtc(D(2026, 11, 1)));
        Assert.Equal(new DateTime(2026, 7, 31, 22, 0, 0, DateTimeKind.Utc), _calendar.StartOfDayUtc(D(2026, 8, 1)));
    }

    // ── Configuration ────────────────────────────────────────────────────────────

    [Fact]
    public void ConfiguredBoundaries_AreUsed()
    {
        var config = Substitute.For<IConfiguration>();
        config["Alerts:Seasons:SpringBuildUpStarts"].Returns("03-01");
        var calendar = TestSeasons.Calendar(config);

        Assert.Equal(SeasonPhase.Winter, calendar.For(D(2026, 2, 20), 0).Phase);
        Assert.Equal(SeasonPhase.SpringBuildUp, calendar.For(D(2026, 3, 1), 0).Phase);
    }

    [Fact]
    public void BackwardsConfiguration_FallsBackToTheDefaults_AsAWhole()
    {
        var config = Substitute.For<IConfiguration>();
        config["Alerts:Seasons:MainSeasonStarts"].Returns("01-10"); // before spring — would run backwards
        var calendar = TestSeasons.Calendar(config);

        Assert.Equal(SeasonPhase.SpringBuildUp, calendar.For(D(2026, 3, 1), 0).Phase);
        Assert.Equal(SeasonPhase.MainSeason, calendar.For(D(2026, 4, 16), 0).Phase);
    }
}
