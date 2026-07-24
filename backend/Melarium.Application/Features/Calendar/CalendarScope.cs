namespace Melarium.Application.Features.Calendar;

/// <summary>
/// The set of apiaries/hives a calendar user may see, plus name lookups — the resolved access scope
/// shared by the in-app calendar, the ICS feed, and the daily agenda so authorization lives in one place.
/// </summary>
public sealed class CalendarScope
{
    public HashSet<int> ApiaryIds { get; init; } = new();
    public HashSet<int> BeehiveIds { get; init; } = new();
    public Dictionary<int, string> BeehiveNames { get; init; } = new();
    public Dictionary<int, string> ApiaryNames { get; init; } = new();
}
