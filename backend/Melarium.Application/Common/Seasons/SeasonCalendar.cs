using System.Globalization;
using Melarium.Domain.Common;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Common.Seasons;

/// <summary>
/// The beekeeping year for continental Bosnia (SPEC-29), with each organization's altitude shift.
///
/// <para>
/// <b>The shift squeezes the season, it does not slide it.</b> One number, −14…+30: the two spring
/// boundaries move <i>later</i> by it and the two autumn boundaries <i>earlier</i>, while 1 August stays
/// put. Higher ground has a later spring <i>and</i> an earlier winter; sliding every boundary the same
/// way would give a mountain apiary a later winter than the valley below it (ADR-046).
/// </para>
///
/// <para>
/// Default phase starts live in <c>Alerts:Seasons</c> as <c>MM-dd</c>. A configuration that would run
/// backwards at either end of the shift range is replaced by the defaults as a whole — mixing
/// configured and default boundaries could make a phase vanish.
/// </para>
/// </summary>
public sealed class SeasonCalendar : ISeasonCalendar
{
    public const int MinShiftDays = -14;
    public const int MaxShiftDays = 30;

    private static readonly MonthDay DefaultSpring     = new(2, 16);
    private static readonly MonthDay DefaultMain       = new(4, 16);
    private static readonly MonthDay DefaultLateSummer = new(8, 1);
    private static readonly MonthDay DefaultWintering  = new(10, 1);
    private static readonly MonthDay DefaultWinter     = new(11, 15);

    private readonly TimeZoneInfo _tz;
    private readonly MonthDay _spring, _main, _lateSummer, _wintering, _winter;

    public SeasonCalendar(IConfiguration config)
    {
        _tz = AppTimeZone.Resolve(config);

        var spring     = Parse(config["Alerts:Seasons:SpringBuildUpStarts"], DefaultSpring);
        var main       = Parse(config["Alerts:Seasons:MainSeasonStarts"], DefaultMain);
        var lateSummer = Parse(config["Alerts:Seasons:LateSummerStarts"], DefaultLateSummer);
        var wintering  = Parse(config["Alerts:Seasons:WinteringStarts"], DefaultWintering);
        var winter     = Parse(config["Alerts:Seasons:WinterStarts"], DefaultWinter);

        if (!Consistent(spring, main, lateSummer, wintering, winter))
            (spring, main, lateSummer, wintering, winter) =
                (DefaultSpring, DefaultMain, DefaultLateSummer, DefaultWintering, DefaultWinter);

        (_spring, _main, _lateSummer, _wintering, _winter) = (spring, main, lateSummer, wintering, winter);
    }

    public DateOnly LocalDate(DateTime utc) => ReportPeriod.LocalDateOf(utc, _tz);

    public SeasonInfo For(DateOnly localDate, int shiftDays)
    {
        var shift = Clamp(shiftDays);
        var b = Boundaries(localDate.Year, shift);

        if (localDate < b.Spring)
            return new(SeasonPhase.Winter, Boundaries(localDate.Year - 1, shift).Winter, b.Spring.AddDays(-1),
                SeasonPhase.SpringBuildUp, b.Spring);
        if (localDate < b.Main)
            return new(SeasonPhase.SpringBuildUp, b.Spring, b.Main.AddDays(-1), SeasonPhase.MainSeason, b.Main);
        if (localDate < b.LateSummer)
            return new(SeasonPhase.MainSeason, b.Main, b.LateSummer.AddDays(-1), SeasonPhase.LateSummer, b.LateSummer);
        if (localDate < b.Wintering)
            return new(SeasonPhase.LateSummer, b.LateSummer, b.Wintering.AddDays(-1), SeasonPhase.Wintering, b.Wintering);
        if (localDate < b.Winter)
            return new(SeasonPhase.Wintering, b.Wintering, b.Winter.AddDays(-1), SeasonPhase.Winter, b.Winter);

        var nextSpring = Boundaries(localDate.Year + 1, shift).Spring;
        return new(SeasonPhase.Winter, b.Winter, nextSpring.AddDays(-1), SeasonPhase.SpringBuildUp, nextSpring);
    }

    public IReadOnlyList<SeasonPhaseRange> CycleFor(DateOnly localDate, int shiftDays)
    {
        var shift = Clamp(shiftDays);
        var year = localDate >= Boundaries(localDate.Year, shift).Winter ? localDate.Year + 1 : localDate.Year;
        var prev = Boundaries(year - 1, shift);
        var b = Boundaries(year, shift);

        return
        [
            new(SeasonPhase.Winter,        prev.Winter,  b.Spring.AddDays(-1)),
            new(SeasonPhase.SpringBuildUp, b.Spring,     b.Main.AddDays(-1)),
            new(SeasonPhase.MainSeason,    b.Main,       b.LateSummer.AddDays(-1)),
            new(SeasonPhase.LateSummer,    b.LateSummer, b.Wintering.AddDays(-1)),
            new(SeasonPhase.Wintering,     b.Wintering,  b.Winter.AddDays(-1)),
        ];
    }

    public DateTime StartOfDayUtc(DateOnly localDate)
    {
        var local = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (_tz.IsInvalidTime(local)) local = local.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(local, _tz);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private Starts Boundaries(int year, int shift) => new(
        _spring.In(year).AddDays(shift),
        _main.In(year).AddDays(shift),
        _lateSummer.In(year),
        _wintering.In(year).AddDays(-shift),
        _winter.In(year).AddDays(-shift));

    private static int Clamp(int shift) => Math.Clamp(shift, MinShiftDays, MaxShiftDays);

    /// <summary>Strictly increasing within one calendar year at both ends of the shift range.</summary>
    private static bool Consistent(MonthDay spring, MonthDay main, MonthDay lateSummer, MonthDay wintering, MonthDay winter)
    {
        foreach (var shift in new[] { MinShiftDays, MaxShiftDays })
        {
            const int year = 2001; // not a leap year — the tighter case
            var s = spring.In(year).AddDays(shift);
            var m = main.In(year).AddDays(shift);
            var l = lateSummer.In(year);
            var wg = wintering.In(year).AddDays(-shift);
            var w = winter.In(year).AddDays(-shift);

            if (s.Year != year || w.Year != year) return false;
            if (!(s < m && m < l && l < wg && wg < w)) return false;
        }
        return true;
    }

    private static MonthDay Parse(string? raw, MonthDay fallback)
    {
        if (string.IsNullOrWhiteSpace(raw)) return fallback;

        var parts = raw.Trim().Split('-');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var day)
            || month is < 1 or > 12
            || day < 1 || day > DateTime.DaysInMonth(2000, month))
            return fallback;

        return new MonthDay(month, day);
    }

    private readonly record struct MonthDay(int Month, int Day)
    {
        // 29 February falls back to the 28th in a common year instead of throwing.
        public DateOnly In(int year) => new(year, Month, Math.Min(Day, DateTime.DaysInMonth(year, Month)));
    }

    private readonly record struct Starts(
        DateOnly Spring, DateOnly Main, DateOnly LateSummer, DateOnly Wintering, DateOnly Winter);
}
