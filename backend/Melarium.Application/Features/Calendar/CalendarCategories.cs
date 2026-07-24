namespace Melarium.Application.Features.Calendar;

/// <summary>
/// Which obligation categories to include when gathering — driven by the user's <c>CalendarSettings</c>
/// toggles. Feedings and todos mirror the existing in-app calendar; treatments and inspections add the
/// derived deadlines (SPEC-11 "sve obaveze").
/// </summary>
public sealed record CalendarCategories(bool Feedings, bool Todos, bool Treatments, bool Inspections)
{
    public static readonly CalendarCategories All = new(true, true, true, true);
}
