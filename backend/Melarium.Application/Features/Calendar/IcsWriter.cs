using System.Globalization;
using System.Text;

namespace Melarium.Application.Features.Calendar;

/// <summary>
/// Hand-rolled RFC 5545 (iCalendar) writer — no NuGet dependency. Emits one all-day <c>VEVENT</c> per
/// obligation with a <c>VALARM</c> whose absolute UTC trigger equals the given local reminder hour on
/// the event day (DST-aware). Handles the format's fiddly bits: value escaping, CRLF line endings, and
/// 75-octet line folding that never splits a multibyte (UTF-8) character.
/// </summary>
public static class IcsWriter
{
    private const string UtcStampFormat = "yyyyMMdd'T'HHmmss'Z'";

    public static string Build(
        IReadOnlyList<CalendarObligation> items,
        TimeZoneInfo timeZone,
        string ianaTimeZoneId,
        int reminderLocalHour,
        string uidHost,
        string calendarName)
    {
        var sb = new StringBuilder();
        void Line(string s) => sb.Append(Fold(s)).Append("\r\n");

        Line("BEGIN:VCALENDAR");
        Line("VERSION:2.0");
        Line("PRODID:-//Melarium//Calendar Feed//BS");
        Line("CALSCALE:GREGORIAN");
        Line("METHOD:PUBLISH");
        Line($"X-WR-CALNAME:{Escape(calendarName)}");
        Line($"X-WR-TIMEZONE:{Escape(ianaTimeZoneId)}");
        Line("REFRESH-INTERVAL;VALUE=DURATION:PT12H");
        Line("X-PUBLISHED-TTL:PT12H");

        var stamp = DateTime.UtcNow.ToString(UtcStampFormat, CultureInfo.InvariantCulture);

        foreach (var it in items)
        {
            var start = it.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var end   = it.Date.AddDays(1).ToString("yyyyMMdd", CultureInfo.InvariantCulture);

            // Reminder: local reminderLocalHour on the event day, expressed as an absolute UTC trigger.
            var localTrigger = new DateTime(it.Date.Year, it.Date.Month, it.Date.Day,
                                            reminderLocalHour, 0, 0, DateTimeKind.Unspecified);
            var utcTrigger = TimeZoneInfo.ConvertTimeToUtc(localTrigger, timeZone)
                                         .ToString(UtcStampFormat, CultureInfo.InvariantCulture);

            Line("BEGIN:VEVENT");
            Line($"UID:{it.StableKey}@{uidHost}");
            Line($"DTSTAMP:{stamp}");
            Line($"DTSTART;VALUE=DATE:{start}");
            Line($"DTEND;VALUE=DATE:{end}");
            Line($"SUMMARY:{Escape(it.Title)}");
            if (!string.IsNullOrWhiteSpace(it.Description)) Line($"DESCRIPTION:{Escape(it.Description!)}");
            if (!string.IsNullOrWhiteSpace(it.Location))    Line($"LOCATION:{Escape(it.Location!)}");
            Line("STATUS:CONFIRMED");
            Line("BEGIN:VALARM");
            Line("ACTION:DISPLAY");
            Line($"DESCRIPTION:{Escape(it.Title)}");
            Line($"TRIGGER;VALUE=DATE-TIME:{utcTrigger}");
            Line("END:VALARM");
            Line("END:VEVENT");
        }

        Line("END:VCALENDAR");
        return sb.ToString();
    }

    /// <summary>Escapes RFC 5545 TEXT values: backslash first, then ; , and newlines.</summary>
    internal static string Escape(string s) => s
        .Replace("\\", "\\\\")
        .Replace(";", "\\;")
        .Replace(",", "\\,")
        .Replace("\r\n", "\\n")
        .Replace("\n", "\\n")
        .Replace("\r", "\\n");

    /// <summary>
    /// Folds a content line to ≤ 75 octets per RFC 5545, continuing with CRLF + a single space.
    /// Iterates by rune so a multibyte UTF-8 character is never split across the fold.
    /// </summary>
    internal static string Fold(string line)
    {
        if (Encoding.UTF8.GetByteCount(line) <= 75) return line;

        var sb = new StringBuilder(line.Length + 16);
        var col = 0;
        foreach (var rune in line.EnumerateRunes())
        {
            var runeBytes = rune.Utf8SequenceLength;
            if (col + runeBytes > 75)
            {
                sb.Append("\r\n ");
                col = 1; // the leading space counts toward the 75-octet continuation line
            }
            sb.Append(rune.ToString());
            col += runeBytes;
        }
        return sb.ToString();
    }
}
