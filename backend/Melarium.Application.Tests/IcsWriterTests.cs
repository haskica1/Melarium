using System.Text;
using Melarium.Application.Features.Calendar;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// The hand-rolled ICS writer (SPEC-11): stable UIDs, all-day dates, DST-correct 08:00 alarms,
/// RFC 5545 escaping, and 75-octet folding that never splits a multibyte character.
/// </summary>
public class IcsWriterTests
{
    private static readonly TimeZoneInfo Sarajevo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    private static CalendarObligation Item(
        DateOnly date, string title = "Prihrana", string key = "feeding-1", string? desc = null, string? loc = null)
        => new(ObligationKind.Feeding, key, date, title, desc, loc, 1, null, false);

    private static string Build(params CalendarObligation[] items)
        => IcsWriter.Build(items, Sarajevo, "Europe/Sarajevo", 8, "beehive.app", "Melarium — obaveze");

    [Fact]
    public void Wraps_In_Vcalendar_With_Crlf()
    {
        var ics = Build(Item(new DateOnly(2026, 7, 15)));
        Assert.StartsWith("BEGIN:VCALENDAR\r\n", ics);
        Assert.EndsWith("END:VCALENDAR\r\n", ics);
    }

    [Fact]
    public void Emits_Stable_Uid_And_AllDay_Dates()
    {
        var ics = Build(Item(new DateOnly(2026, 7, 15), key: "feeding-42"));
        Assert.Contains("UID:feeding-42@beehive.app\r\n", ics);
        Assert.Contains("DTSTART;VALUE=DATE:20260715\r\n", ics);
        Assert.Contains("DTEND;VALUE=DATE:20260716\r\n", ics);
    }

    [Fact]
    public void Summer_Alarm_Is_08_Local_Which_Is_06_Utc()
    {
        // CEST = UTC+2 in July → 08:00 local == 06:00Z
        var ics = Build(Item(new DateOnly(2026, 7, 15)));
        Assert.Contains("TRIGGER;VALUE=DATE-TIME:20260715T060000Z\r\n", ics);
    }

    [Fact]
    public void Winter_Alarm_Is_08_Local_Which_Is_07_Utc()
    {
        // CET = UTC+1 in January → 08:00 local == 07:00Z
        var ics = Build(Item(new DateOnly(2026, 1, 15)));
        Assert.Contains("TRIGGER;VALUE=DATE-TIME:20260115T070000Z\r\n", ics);
    }

    [Fact]
    public void Escapes_Reserved_Characters()
    {
        var ics = Build(Item(new DateOnly(2026, 7, 15), title: "A, B; C\\ D", desc: "l1\nl2"));
        Assert.Contains("SUMMARY:A\\, B\\; C\\\\ D\r\n", ics);
        Assert.Contains("DESCRIPTION:l1\\nl2\r\n", ics);
    }

    [Fact]
    public void Folds_Every_Line_To_75_Octets()
    {
        var ics = Build(Item(new DateOnly(2026, 7, 15), title: new string('x', 200)));
        foreach (var line in ics.Split("\r\n"))
            Assert.True(Encoding.UTF8.GetByteCount(line) <= 75, $"Line exceeds 75 octets ({Encoding.UTF8.GetByteCount(line)}): {line}");
    }

    [Fact]
    public void Folding_Never_Splits_A_Multibyte_Char()
    {
        var honey = string.Concat(Enumerable.Repeat("🍯", 40)); // 4 bytes each → forces several folds
        var ics = Build(Item(new DateOnly(2026, 7, 15), title: honey));

        Assert.DoesNotContain('�', ics);                 // no broken UTF-8 sequences
        Assert.Contains(honey, ics.Replace("\r\n ", ""));      // unfolding restores the original run
    }

    [Fact]
    public void One_Vevent_Per_Obligation()
    {
        var ics = Build(
            Item(new DateOnly(2026, 7, 15), key: "feeding-1"),
            Item(new DateOnly(2026, 7, 17), key: "feeding-2"));
        Assert.Equal(2, ics.Split("BEGIN:VEVENT").Length - 1);
        Assert.Equal(2, ics.Split("BEGIN:VALARM").Length - 1);
    }
}
