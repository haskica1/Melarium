using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Common;

/// <summary>
/// Resolves the single application-wide time zone (SPEC-11). The whole app stores UTC; "08:00 local"
/// reminders and all-day calendar events need one configured zone to convert against (DST-aware).
/// Per-user zones are out of scope for v1. Defaults to <c>Europe/Sarajevo</c>; an unknown/absent id
/// falls back to UTC so a misconfiguration never crashes a background worker.
/// </summary>
public static class AppTimeZone
{
    public const string DefaultId = "Europe/Sarajevo";

    public static string IanaId(IConfiguration config)
    {
        var id = config["App:TimeZone"];
        return string.IsNullOrWhiteSpace(id) ? DefaultId : id;
    }

    public static TimeZoneInfo Resolve(IConfiguration config)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(IanaId(config));
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>Today's date in the app zone (the calendar/agenda "today").</summary>
    public static DateOnly Today(TimeZoneInfo tz) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz));
}
