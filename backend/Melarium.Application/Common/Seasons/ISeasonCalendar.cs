namespace Melarium.Application.Common.Seasons;

/// <summary>
/// The beekeeping year (SPEC-29). The phase is <b>derived</b> — from the date in the application time
/// zone and the organization's <c>SeasonShiftDays</c> — never stored, following the effective-plan and
/// karenca precedent: nothing has to flip a flag on the first day of spring.
/// </summary>
public interface ISeasonCalendar
{
    /// <summary>The calendar date an instant falls on in <c>App:TimeZone</c>.</summary>
    DateOnly LocalDate(DateTime utc);

    /// <summary>The phase containing <paramref name="localDate"/> for an organization with this shift.</summary>
    SeasonInfo For(DateOnly localDate, int shiftDays);

    /// <summary>
    /// The five phases of the beekeeping year containing <paramref name="localDate"/>, in order,
    /// starting with the winter that opens it. December therefore belongs to the next year's list.
    /// </summary>
    IReadOnlyList<SeasonPhaseRange> CycleFor(DateOnly localDate, int shiftDays);

    /// <summary>The UTC instant at which a local calendar day begins — for "since the phase started" queries.</summary>
    DateTime StartOfDayUtc(DateOnly localDate);
}
