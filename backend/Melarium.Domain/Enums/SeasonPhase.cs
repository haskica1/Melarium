namespace Melarium.Domain.Enums;

/// <summary>
/// The five phases of the beekeeping year (SPEC-29). Never stored: the phase is derived from the
/// local date and the organization's <c>SeasonShiftDays</c> by <c>ISeasonCalendar</c>, the same way
/// the effective plan and a treatment's karenca are derived rather than flipped by a job.
/// Numbered in the order the year runs, starting from winter.
/// </summary>
public enum SeasonPhase
{
    Winter        = 1,
    SpringBuildUp = 2,
    MainSeason    = 3,
    LateSummer    = 4,
    Wintering     = 5,
}
