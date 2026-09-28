using System.Globalization;
using System.Text.RegularExpressions;
using Melarium.Application.Common.Email;
using Melarium.Application.Common.Localization;
using Melarium.Application.Features.Calendar;
using Melarium.Application.Features.Notifications;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Reminders;

/// <summary>
/// Composes the one morning e-mail (SPEC-29) as structured content (ADR-048): the day's numbers,
/// "Traži pažnju" — one card per alert, a grouped alert's hives as rows — then today's obligations as
/// a checklist, then what is there to read (the season phase that started, the AI summary). Pure, so
/// what the user receives is tested without the worker.
/// </summary>
public static class MorningEmail
{
    // Grouped hive alerts list one hive per line: "- K2 (34 dana)" → name and detail.
    private static readonly Regex HiveLine = new(@"^(?<name>.+?) \((?<detail>[^()]+)\)$", RegexOptions.Compiled);

    private static readonly HashSet<NotificationType> HiveGroups =
        [NotificationType.InspectionOverdue, NotificationType.HoneyLevelDrop, NotificationType.OldQueen];

    private static readonly HashSet<NotificationType> Reading =
        [NotificationType.SeasonPhaseStarted, NotificationType.WeeklySummary];

    public static EmailContent Compose(
        DateOnly today,
        IReadOnlyList<CalendarObligation> obligations,
        IReadOnlyList<Notification> alerts,
        string? firstName)
    {
        var attention = alerts.Where(a => !Reading.Contains(a.Type)).ToList();
        var reading = alerts.Where(a => Reading.Contains(a.Type)).ToList();
        var blocks = new List<EmailBlock> { new EmailText(Lead(obligations.Count, attention.Count)) };

        if (obligations.Count > 0 && attention.Count > 0)
        {
            blocks.Add(new EmailStats(
            [
                new(obligations.Count.ToString(CultureInfo.InvariantCulture),
                    BsLabels.Word(obligations.Count, "obaveza danas", "obaveze danas", "obaveza danas")),
                new(attention.Count.ToString(CultureInfo.InvariantCulture),
                    BsLabels.Word(attention.Count, "traži pažnju", "traže pažnju", "traži pažnju"), EmailTone.Warning),
            ]));
        }

        if (attention.Count > 0)
        {
            blocks.Add(new EmailSection("Traži pažnju", attention.Count));
            blocks.AddRange(attention.Select(AlertCard));
        }

        if (obligations.Count > 0)
        {
            blocks.Add(new EmailSection("Današnje obaveze", obligations.Count));
            blocks.Add(new EmailChecklist(obligations.Select(ObligationRow).ToList()));
        }

        if (reading.Count > 0)
        {
            blocks.Add(new EmailSection("Za čitanje"));
            blocks.AddRange(reading.Select(ReadingCard));
        }

        return new EmailContent(firstName is { Length: > 0 } name ? $"Dobro jutro, {name}" : "Dobro jutro")
        {
            Subject = Subject(obligations.Count, attention.Count, reading),
            Eyebrow = $"Jutarnji pregled · {BsLabels.LongDate(today)}",
            Icon = "☀️",
            Greet = false,
            Blocks = blocks,
            Button = new EmailLink("/", "Otvori Melarium"),
            Reason = "Jutarnji pregled stiže u 08:00 kad za taj dan ima obaveza ili upozorenja.",
            LinkSettings = true,
            Preheader = Preheader(attention, reading, obligations),
        };
    }

    // ── Text ──────────────────────────────────────────────────────────────────

    private static string Lead(int obligations, int attention)
    {
        var todo = obligations == 0 ? null
            : $"Danas te {BsLabels.Word(obligations, "čeka", "čekaju", "čeka")} {BsLabels.Count(obligations, "obaveza", "obaveze", "obaveza")}";
        var alerts = attention == 0 ? null
            : $"{BsLabels.Count(attention, "stvar", "stvari", "stvari")} {BsLabels.Word(attention, "traži", "traže", "traži")} pažnju";

        return (todo, alerts) switch
        {
            (not null, not null) => $"{todo}, a {alerts}.",
            (not null, null)     => $"{todo}.",
            (null, not null)     => $"Danas nema obaveza u kalendaru, ali {alerts}.",
            _                    => "Danas nema obaveza ni upozorenja — samo nešto za čitanje.",
        };
    }

    private static string Subject(int obligations, int attention, IReadOnlyList<Notification> reading)
    {
        var parts = new List<string>();
        if (obligations > 0) parts.Add(BsLabels.Count(obligations, "obaveza", "obaveze", "obaveza"));
        if (attention > 0) parts.Add(BsLabels.Count(attention, "upozorenje", "upozorenja", "upozorenja"));
        if (parts.Count == 0 && reading.Count > 0) parts.Add(reading[0].Title);
        return parts.Count == 0 ? "Jutarnji pregled" : $"Jutarnji pregled: {string.Join(", ", parts)}";
    }

    private static string Preheader(
        IReadOnlyList<Notification> attention, IReadOnlyList<Notification> reading, IReadOnlyList<CalendarObligation> obligations)
    {
        var titles = attention.Concat(reading).Select(a => a.Title).ToList();
        if (titles.Count == 0) titles = obligations.Select(o => ObligationRow(o).Name).ToList();
        return string.Join(" · ", titles);
    }

    // ── Cards ─────────────────────────────────────────────────────────────────

    private static EmailCard AlertCard(Notification alert)
    {
        var message = Parse(alert.Message);
        var card = new EmailCard(alert.Title)
        {
            Icon = NotificationEmail.Icon(alert.Type),
            Tone = EmailTone.Warning,
            Link = NotificationEmail.Button(alert.Type, alert.RelatedEntityId, alert.RelatedEntityType),
        };

        if (message.Items.Count == 0) return card with { Text = alert.Message.Trim() };

        // A grouped alert: its sentence becomes the subtitle and each hive a row.
        return HiveGroups.Contains(alert.Type)
            ? card with { Subtitle = message.Lead?.TrimEnd(':'), Rows = message.Items.Select(HiveRow).ToList() }
            : card with { Text = message.Lead?.TrimEnd(':'), Bullets = message.Items };
    }

    private static EmailCard ReadingCard(Notification item)
    {
        var message = Parse(item.Message);

        if (item.Type == NotificationType.SeasonPhaseStarted)
        {
            return new EmailCard(item.Title)
            {
                Icon = NotificationEmail.Icon(item.Type),
                Tone = EmailTone.Success,
                Featured = true,
                Subtitle = message.Lead?.TrimEnd(':'),
                Bullets = message.Items,
                MoreHeading = message.MoreHeading,
                MoreBullets = message.MoreItems,
                Link = NotificationEmail.Button(item.Type, item.RelatedEntityId, item.RelatedEntityType),
            };
        }

        // The AI summary: its bullets, and any sentence it wrote outside them.
        return new EmailCard(item.Title)
        {
            Icon = NotificationEmail.Icon(item.Type),
            Tag = "AI",
            Text = message.Items.Count == 0 ? item.Message.Trim() : message.Lead,
            Bullets = message.Items,
        };
    }

    private static EmailRow HiveRow(string line)
    {
        var match = HiveLine.Match(line);
        return match.Success
            ? new EmailRow(match.Groups["name"].Value, match.Groups["detail"].Value)
            : new EmailRow(line);
    }

    /// <summary>
    /// "🍯 Prehrana — Ozren (8 košnica)" → icon, "Prehrana", "Ozren (8 košnica)". The cut is before the
    /// obligation's location when the title names it, so a dash inside a todo's own title survives.
    /// </summary>
    private static EmailRow ObligationRow(CalendarObligation o)
    {
        var (icon, text) = SplitIcon(o.Title);
        var cut = o.Location is { Length: > 0 } location ? text.LastIndexOf($" — {location}", StringComparison.Ordinal) : -1;
        if (cut < 0) cut = text.LastIndexOf(" — ", StringComparison.Ordinal);

        var url = o.Path
            ?? (o.BeehiveId is int hive ? $"/beehives/{hive}" : o.ApiaryId is int apiary ? $"/apiaries/{apiary}" : null);

        return cut < 0
            ? new EmailRow(text, null, icon ?? KindIcon(o.Kind), url)
            : new EmailRow(text[..cut], text[(cut + 3)..], icon ?? KindIcon(o.Kind), url);
    }

    private static (string? Icon, string Text) SplitIcon(string title)
    {
        var elements = StringInfo.GetTextElementEnumerator(title);
        if (!elements.MoveNext()) return (null, title);

        var first = (string)elements.Current;
        return char.IsLetterOrDigit(first, 0) ? (null, title) : (first, title[first.Length..].Trim());
    }

    private static string KindIcon(ObligationKind kind) => kind switch
    {
        ObligationKind.Feeding => "🍯",
        ObligationKind.Todo => "📋",
        ObligationKind.InspectionDue => "🔍",
        _ => "💊",
    };

    // ── Parsing the stored notification text ──────────────────────────────────

    /// <summary>
    /// The shape <c>AlertRuleService</c> writes: an opening sentence, "- " items, optionally a
    /// "Heading:" line with more items (the phase notice's "Iz Edukacije:").
    /// </summary>
    private sealed record Parsed(string? Lead, List<string> Items, string? MoreHeading, List<string> MoreItems);

    private static Parsed Parse(string message)
    {
        string? lead = null, moreHeading = null;
        List<string> items = [], more = [];

        foreach (var raw in message.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("- "))
                (moreHeading is null ? items : more).Add(line[2..].Trim());
            else if (lead is null && items.Count == 0)
                lead = line;
            else if (moreHeading is null && line.EndsWith(':'))
                moreHeading = line.TrimEnd(':');
            else
                lead = lead is null ? line : $"{lead} {line}";
        }

        return new Parsed(lead, items, moreHeading, more);
    }
}
