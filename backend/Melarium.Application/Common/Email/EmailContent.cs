namespace Melarium.Application.Common.Email;

/// <summary>
/// What an e-mail says, independent of how it is drawn. The sender decides the content and its
/// weight — tone, icon, sections, the one button; <c>EmailTemplate</c> in Infrastructure decides the
/// HTML. A plain notification is a title and one <see cref="EmailText"/>; the morning e-mail is
/// numbers, sections, cards and a checklist. ADR-048.
/// </summary>
/// <remarks>
/// Paths in links ("/beehives/5") are resolved against the app URL when the mail is rendered, so
/// Application code never needs <c>FrontendUrl</c>; an absolute URL (a one-time token link) is used
/// as is. Every string is escaped by the renderer — pass raw user text, never HTML.
/// </remarks>
public sealed record EmailContent(string Title)
{
    /// <summary>The inbox subject; the title when unset. No "Melarium —" prefix — the sender name already says it.</summary>
    public string? Subject { get; init; }

    /// <summary>Small upper-case label above the title — the category ("Zadatak", "Sigurnost računa").</summary>
    public string? Eyebrow { get; init; }

    /// <summary>One emoji in the header tile; the bee when unset.</summary>
    public string? Icon { get; init; }

    public EmailTone Tone { get; init; } = EmailTone.Brand;

    /// <summary>
    /// Opens the body with "Pozdrav, {first name}" when the recipient is an account. Off for mail that
    /// greets in its title (the morning e-mail) and for operator mail.
    /// </summary>
    public bool Greet { get; init; } = true;

    public IReadOnlyList<EmailBlock> Blocks { get; init; } = [];

    public EmailLink? Button { get; init; }

    /// <summary>Prints the button's URL under it — for one-time links (reset, verify) that must work even when the button doesn't.</summary>
    public bool ShowLinkFallback { get; init; }

    /// <summary>Blocks under the button: the "not you?" note that only matters if the button is the wrong move.</summary>
    public IReadOnlyList<EmailBlock> After { get; init; } = [];

    /// <summary>Why this arrived — the footer's first line.</summary>
    public string? Reason { get; init; }

    /// <summary>Adds the link to the notification settings after <see cref="Reason"/>.</summary>
    public bool LinkSettings { get; init; }

    /// <summary>Operator mail: no "need help?" line — the reader is the help.</summary>
    public bool Internal { get; init; }

    /// <summary>The inbox preview line; derived from the first text block when unset.</summary>
    public string? Preheader { get; init; }

    /// <summary>A title and plain text, optionally a button — the shape every notification had before ADR-048.</summary>
    public static EmailContent Simple(string title, string text, EmailLink? button = null) =>
        new(title) { Blocks = [new EmailText(text)], Button = button };
}

public enum EmailTone
{
    /// <summary>Honey — the everyday colour.</summary>
    Brand,
    /// <summary>Red — Critical alerts and account security.</summary>
    Critical,
    /// <summary>Orange — the morning e-mail's "needs attention" cards.</summary>
    Warning,
    /// <summary>Green — good news: a reward, a published topic, a resolved report.</summary>
    Success,
    /// <summary>Grey — removals and refusals, nothing to act on urgently.</summary>
    Neutral,
}

/// <summary>A path ("/beehives/5") is resolved against the app URL; an absolute URL is used as is.</summary>
public sealed record EmailLink(string Url, string Label);

public abstract record EmailBlock;

/// <summary>Plain text: blank lines split paragraphs, "- " lines become a list — the notification format.</summary>
public sealed record EmailText(string Text) : EmailBlock;

/// <summary>Label/value rows: "Paket — Pro", "Ističe — 30.09.2026.".</summary>
public sealed record EmailFacts(IReadOnlyList<EmailFact> Rows) : EmailBlock;

/// <summary>
/// One row of <see cref="EmailFacts"/>. <see cref="SentAt"/> prints the local date and time the mail
/// goes out instead of <see cref="Value"/> — the "when" of a security notice, known to the sender
/// without every service needing a clock and the app time zone.
/// </summary>
public sealed record EmailFact(string Label, string Value = "", bool SentAt = false);

/// <summary>A boxed note: "Link vrijedi 2 sata", "Podaci se ne brišu".</summary>
public sealed record EmailCallout(string Text, EmailTone Tone = EmailTone.Neutral, string? Icon = null) : EmailBlock;

/// <summary>A row of big numbers — the morning e-mail's "4 obaveze · 3 traže pažnju".</summary>
public sealed record EmailStats(IReadOnlyList<EmailStat> Items) : EmailBlock;
public sealed record EmailStat(string Value, string Label, EmailTone Tone = EmailTone.Brand);

/// <summary>An upper-case section heading, optionally with a count badge.</summary>
public sealed record EmailSection(string Title, int? Count = null) : EmailBlock;

/// <summary>
/// A bordered card: icon tile, title, subtitle, then text, name/detail rows or bullets, and a link.
/// <see cref="Featured"/> tints it — the season notice, not the routine alerts.
/// </summary>
public sealed record EmailCard(string Title) : EmailBlock
{
    public string? Icon { get; init; }
    public EmailTone Tone { get; init; } = EmailTone.Brand;
    public string? Subtitle { get; init; }
    /// <summary>A small badge after the title ("AI").</summary>
    public string? Tag { get; init; }
    public string? Text { get; init; }
    public IReadOnlyList<EmailRow> Rows { get; init; } = [];
    public IReadOnlyList<string> Bullets { get; init; } = [];
    public string? MoreHeading { get; init; }
    public IReadOnlyList<string> MoreBullets { get; init; } = [];
    public EmailLink? Link { get; init; }
    public bool Featured { get; init; }
}

/// <summary>One line of a card or checklist: "K2 · 34 dana", "🍯 Prehrana · Ozren (8 košnica)".</summary>
public sealed record EmailRow(string Name, string? Detail = null, string? Icon = null, string? Url = null);

/// <summary>Rows with an icon each, one under another — today's obligations, the first steps.</summary>
public sealed record EmailChecklist(IReadOnlyList<EmailRow> Items) : EmailBlock;
