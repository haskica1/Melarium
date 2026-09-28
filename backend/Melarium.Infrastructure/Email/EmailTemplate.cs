using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Melarium.Application.Common;
using Melarium.Application.Common.Email;
using Melarium.Application.Common.Localization;
using Microsoft.Extensions.Configuration;

namespace Melarium.Infrastructure.Email;

/// <summary>What the renderer needs besides the content — resolved once per send by the worker.</summary>
/// <param name="AppUrl">The frontend's base URL; paths in links are resolved against it.</param>
/// <param name="LogoUrl">The app icon, hosted (Gmail blocks data: images).</param>
/// <param name="FirstName">The account's first name for the greeting; null for operator mail.</param>
/// <param name="LocalNow">The send time in the app time zone, for <see cref="EmailFact.SentAt"/>.</param>
public sealed record EmailContext(string AppUrl, string LogoUrl, string? FirstName, DateTime LocalNow)
{
    /// <summary>The app URL and logo from <c>FrontendUrl</c>, the time in the app zone.</summary>
    public static EmailContext Create(IConfiguration config, string? firstName, DateTime utcNow) => new(
        FrontendUrl.Build(config, ""),
        FrontendUrl.Build(config, "/pwa-192x192.png"),
        firstName,
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), AppTimeZone.Resolve(config)));
}

/// <summary>
/// Draws <see cref="EmailContent"/> as the one Melarium e-mail (ADR-048): the app icon and wordmark,
/// a card whose tinted header carries the icon, category and title, the body blocks, one button, and
/// a footer that says why the mail came and where to change that. Every outbound mail goes through
/// here — notifications, the morning e-mail, reset/verify, operator mail, the SMTP test — so the brand
/// lives in one place.
/// </summary>
/// <remarks>
/// Table layout and inline styles, because Outlook renders with Word; a <c>prefers-color-scheme</c>
/// block for Apple Mail, iOS and Outlook.com dark mode; a max-width block for phones. No web fonts: an
/// @import would disclose the reader's IP to Google on open, and Gmail strips it anyway — the stacks
/// below are what clients were really using. Blocks carry no outer margins; <see cref="Gap"/> decides
/// the space between two neighbours, so a new block type cannot double up spacing.
/// </remarks>
public static class EmailTemplate
{
    private const string Sans = "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif";
    private const string Serif = "'Playfair Display',Georgia,'Times New Roman',serif";

    // Light colours. The dark ones live in the <style> block, keyed by the class names used below.
    private const string Page = "#f6f1e7";
    private const string CardBg = "#ffffff";
    private const string CardLine = "#eae1cf";
    private const string Strong = "#1c1917";
    private const string Body = "#44403c";
    private const string Muted = "#78716c";
    private const string Faint = "#a8a29e";
    private const string Hairline = "#f0e9dc";
    private const string Soft = "#fbf8f1";
    private const string Link = "#b45309";
    private const string Button = "#d97706";

    private const string SettingsPath = "/profile#obavjestenja";

    private sealed record Palette(string Tint, string TintLine, string Tile, string Accent);

    private static Palette For(EmailTone tone) => tone switch
    {
        EmailTone.Critical => new("#fff1f1", "#f7d4d4", "#fddcdc", "#b91c1c"),
        EmailTone.Warning  => new("#fff5eb", "#f6dcc1", "#fde3c8", "#c2410c"),
        EmailTone.Success  => new("#effaf3", "#cdebd7", "#d5f2df", "#15803d"),
        EmailTone.Neutral  => new("#f7f5f2", "#e8e3da", "#ece7df", "#57534e"),
        _                  => new("#fff7e6", "#f3e1b5", "#fde7ad", "#b45309"),
    };

    private static string Key(EmailTone tone) => tone.ToString().ToLowerInvariant();

    // ── HTML ──────────────────────────────────────────────────────────────────

    public static string Render(EmailContent c, EmailContext ctx)
    {
        var tone = For(c.Tone);
        var t = Key(c.Tone);
        var home = E(Resolve("/", ctx));
        var body = new StringBuilder();

        if (c.Greet && ctx.FirstName is { Length: > 0 } firstName)
            body.Append($"""<p class="strong" style="margin:0 0 14px;font-size:16px;line-height:1.5;font-weight:600;color:{Strong};">Pozdrav, {E(firstName)}</p>""");

        AppendBlocks(body, c.Blocks, ctx);

        if (c.Button is { } button)
        {
            var url = E(Resolve(button.Url, ctx));
            body.Append($"""
                <table role="presentation" cellpadding="0" cellspacing="0" border="0" class="btn-table" style="margin:32px 0 0;">
                  <tr>
                    <td class="btn-cell" align="center" bgcolor="{Button}" style="border-radius:12px;background:{Button};">
                      <a class="btn" href="{url}" target="_blank" style="display:inline-block;padding:15px 30px;font-family:{Sans};font-size:16px;font-weight:700;line-height:1.2;color:#ffffff;text-decoration:none;border-radius:12px;">{E(button.Label)}&nbsp;&rarr;</a>
                    </td>
                  </tr>
                </table>
                """);

            if (c.ShowLinkFallback)
            {
                body.Append($"""
                    <p class="muted" style="margin:22px 0 0;font-size:13px;line-height:1.55;color:{Muted};">Dugme ne radi? Kopirajte ovaj link u preglednik:</p>
                    <p style="margin:6px 0 0;font-size:12.5px;line-height:1.5;word-break:break-all;"><a class="link" href="{url}" target="_blank" style="color:{Link};text-decoration:underline;">{url}</a></p>
                    """);
            }
        }

        if (c.After.Count > 0)
        {
            body.Append(Spacer(28));
            AppendBlocks(body, c.After, ctx);
        }

        var eyebrow = c.Eyebrow is { Length: > 0 } label
            ? $"""<p class="accent-{t}" style="margin:22px 0 0;font-family:{Sans};font-size:12px;line-height:1.4;font-weight:700;letter-spacing:.09em;text-transform:uppercase;color:{tone.Accent};">{E(label)}</p>"""
            : string.Empty;
        var titleTop = eyebrow.Length > 0 ? "8px" : "22px";

        var reason = E(c.Reason is { Length: > 0 } r ? r : "Ovu poruku ste primili jer imate račun u Melariumu.");
        var settings = c.LinkSettings
            ? $""" <a class="link" href="{E(Resolve(SettingsPath, ctx))}" target="_blank" style="color:{Link};font-weight:600;text-decoration:none;">Promijenite postavke obavještenja</a>"""
            : string.Empty;
        var help = c.Internal
            ? string.Empty
            : $"""<p class="muted" style="margin:0 0 16px;font-size:13px;line-height:1.6;color:{Muted};">Trebate pomoć? <a class="link" href="mailto:info@melarium.app" style="color:{Link};text-decoration:none;">info@melarium.app</a></p>""";

        return $$"""
            <!DOCTYPE html>
            <html lang="bs" xmlns="http://www.w3.org/1999/xhtml">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta http-equiv="X-UA-Compatible" content="IE=edge">
            <meta name="color-scheme" content="light dark">
            <meta name="supported-color-schemes" content="light dark">
            <meta name="x-apple-disable-message-reformatting">
            <title>{{E(c.Title)}}</title>
            <!--[if mso]>
            <noscript><xml><o:OfficeDocumentSettings><o:PixelsPerInch>96</o:PixelsPerInch></o:OfficeDocumentSettings></xml></noscript>
            <![endif]-->
            <style>
              body, table, td, a { -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; }
              table, td { mso-table-lspace: 0pt; mso-table-rspace: 0pt; }
              img { -ms-interpolation-mode: bicubic; border: 0; outline: none; text-decoration: none; }
              body { margin: 0; padding: 0; width: 100% !important; background: {{Page}}; }
              a { text-decoration: none; }

              @media (max-width: 620px) {
                .container { width: 100% !important; }
                .outer { padding: 20px 10px 28px !important; }
                .hero { padding: 28px 22px 24px !important; }
                .content { padding: 26px 22px 30px !important; }
                .title { font-size: 25px !important; }
                .btn-table { width: 100% !important; }
                .btn { display: block !important; padding: 16px 20px !important; }
                .stat-gap { width: 8px !important; }
                .stat { padding: 14px 12px !important; }
                .stat-value { font-size: 24px !important; }
                .card-pad { padding: 16px !important; }
                .fact-label { width: 38% !important; }
              }

              @media (prefers-color-scheme: dark) {
                body, .page { background: #020617 !important; }
                .card { background: #0f172a !important; border-color: #1e293b !important; }
                .wordmark { color: #fcd34d !important; }
                .title, .strong { color: #f8fafc !important; }
                .text { color: #cbd5e1 !important; }
                .muted { color: #94a3b8 !important; }
                .faint { color: #64748b !important; }
                .line { border-color: #1e293b !important; }
                .soft { background: #111a2e !important; border-color: #1e293b !important; }
                .link { color: #fbbf24 !important; }
                .dot { color: #fbbf24 !important; }
                .tint-brand { background: #1d1708 !important; border-color: #3a2d0f !important; }
                .tint-critical { background: #230f13 !important; border-color: #4a1d24 !important; }
                .tint-warning { background: #22140a !important; border-color: #4a2a12 !important; }
                .tint-success { background: #0b2016 !important; border-color: #14532d !important; }
                .tint-neutral { background: #141c2e !important; border-color: #243047 !important; }
                .tile-brand { background: #3b2c0c !important; }
                .tile-critical { background: #4c1b22 !important; }
                .tile-warning { background: #4a2a10 !important; }
                .tile-success { background: #134e2c !important; }
                .tile-neutral { background: #1e293b !important; }
                .accent-brand { color: #fbbf24 !important; }
                .accent-critical { color: #f87171 !important; }
                .accent-warning { color: #fb923c !important; }
                .accent-success { color: #4ade80 !important; }
                .accent-neutral { color: #94a3b8 !important; }
                .pill { background: #1e293b !important; color: #fcd34d !important; }
              }
            </style>
            </head>
            <body class="page" style="margin:0;padding:0;background:{{Page}};">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0;mso-hide:all;font-size:1px;line-height:1px;color:{{Page}};">
                {{E(Preheader(c))}}
                &#8203;&zwnj;&nbsp;&#8203;&zwnj;&nbsp;&#8203;&zwnj;&nbsp;&#8203;&zwnj;&nbsp;&#8203;&zwnj;&nbsp;&#8203;&zwnj;&nbsp;&#8203;&zwnj;&nbsp;&#8203;&zwnj;&nbsp;
              </div>

              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" bgcolor="{{Page}}" class="page" style="background:{{Page}};">
                <tr>
                  <td align="center" class="outer" style="padding:32px 16px 40px;">

                    <table role="presentation" width="600" cellpadding="0" cellspacing="0" border="0" class="container" style="width:600px;max-width:600px;">

                      <tr>
                        <td align="left" style="padding:0 6px 18px;">
                          <table role="presentation" cellpadding="0" cellspacing="0" border="0">
                            <tr>
                              <td style="vertical-align:middle;">
                                <a href="{{home}}" target="_blank"><img src="{{E(ctx.LogoUrl)}}" width="36" height="36" alt="" style="display:block;width:36px;height:36px;border-radius:10px;"></a>
                              </td>
                              <td style="vertical-align:middle;padding-left:10px;">
                                <a href="{{home}}" target="_blank" class="wordmark" style="font-family:{{Serif}};font-size:21px;font-weight:700;color:#78350f;text-decoration:none;letter-spacing:.2px;">Melarium</a>
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>

                      <tr>
                        <td class="card" align="left" bgcolor="{{CardBg}}" style="background:{{CardBg}};border:1px solid {{CardLine}};border-radius:20px;">

                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                            <tr>
                              <td class="hero tint-{{t}}" bgcolor="{{tone.Tint}}" style="background:{{tone.Tint}};border-bottom:1px solid {{tone.TintLine}};border-radius:20px 20px 0 0;padding:34px 40px 30px;">
                                <table role="presentation" cellpadding="0" cellspacing="0" border="0">
                                  <tr>
                                    <td class="tile-{{t}}" width="56" height="56" align="center" valign="middle" bgcolor="{{tone.Tile}}" style="width:56px;height:56px;background:{{tone.Tile}};border-radius:16px;font-size:28px;line-height:56px;text-align:center;">{{E(c.Icon ?? "🐝")}}</td>
                                  </tr>
                                </table>
                                {{eyebrow}}
                                <h1 class="title" style="margin:{{titleTop}} 0 0;font-family:{{Serif}};font-size:29px;line-height:1.25;font-weight:700;color:{{Strong}};">{{E(c.Title)}}</h1>
                              </td>
                            </tr>
                            <tr>
                              <td class="content text" style="padding:32px 40px 40px;font-family:{{Sans}};font-size:16px;line-height:1.65;color:{{Body}};">
                                {{body}}
                              </td>
                            </tr>
                          </table>

                        </td>
                      </tr>

                      <tr>
                        <td align="center" style="padding:28px 28px 0;font-family:{{Sans}};">
                          <p class="muted" style="margin:0 0 10px;font-size:13px;line-height:1.6;color:{{Muted}};">{{reason}}{{settings}}</p>
                          {{help}}
                          <p class="faint" style="margin:0;font-size:12px;line-height:1.6;color:{{Faint}};">Melarium · pametno upravljanje pčelinjakom</p>
                        </td>
                      </tr>

                    </table>

                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    // ── Blocks ────────────────────────────────────────────────────────────────

    private static void AppendBlocks(StringBuilder body, IReadOnlyList<EmailBlock> blocks, EmailContext ctx)
    {
        EmailBlock? previous = null;
        foreach (var block in blocks)
        {
            var gap = Gap(previous, block);
            if (gap > 0) body.Append(Spacer(gap));
            body.Append(RenderBlock(block, ctx));
            previous = block;
        }
    }

    private static int Gap(EmailBlock? previous, EmailBlock next) => (previous, next) switch
    {
        (null, _) => 0,
        (_, EmailSection) => 0,       // a section heading brings its own top margin
        (EmailSection, _) => 0,       // and its own bottom one
        (EmailCard, EmailCard) => 0,  // cards stack on their 12 px margin
        (EmailText, EmailText) => 16,
        _ => 20,
    };

    private static string Spacer(int px) =>
        $"""<div style="height:{px}px;line-height:{px}px;font-size:0;">&nbsp;</div>""";

    private static string RenderBlock(EmailBlock block, EmailContext ctx) => block switch
    {
        EmailText text => FormatText(text.Text),
        EmailFacts facts => Facts(facts, ctx),
        EmailCallout callout => Callout(callout),
        EmailStats stats => Stats(stats),
        EmailSection section => Section(section),
        EmailCard card => Card(card, ctx),
        EmailChecklist list => Checklist(list, ctx),
        _ => string.Empty,
    };

    private static string Facts(EmailFacts facts, EmailContext ctx)
    {
        var rows = new StringBuilder();
        for (var i = 0; i < facts.Rows.Count; i++)
        {
            var f = facts.Rows[i];
            var border = i < facts.Rows.Count - 1 ? $"border-bottom:1px solid {Hairline};" : "";
            rows.Append($"""
                <tr>
                  <td class="muted line fact-label" width="32%" valign="top" style="{border}padding:12px 16px;font-size:13.5px;line-height:1.5;color:{Muted};">{E(f.Label)}</td>
                  <td class="strong line" valign="top" style="{border}padding:12px 16px;font-size:14.5px;line-height:1.5;font-weight:600;color:{Strong};">{E(FactValue(f, ctx))}</td>
                </tr>
                """);
        }

        return $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" class="soft" bgcolor="{Soft}" style="background:{Soft};border:1px solid {Hairline};border-radius:14px;border-collapse:separate;">
              {rows}
            </table>
            """;
    }

    private static string Callout(EmailCallout c)
    {
        var p = For(c.Tone);
        var icon = c.Icon is { Length: > 0 } i
            ? $"""<td width="30" valign="top" style="width:30px;font-size:18px;line-height:24px;">{E(i)}</td>"""
            : "";
        return $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
              <tr>
                <td class="tint-{Key(c.Tone)}" bgcolor="{p.Tint}" style="background:{p.Tint};border:1px solid {p.TintLine};border-radius:14px;padding:14px 16px;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"><tr>
                    {icon}
                    <td class="strong" style="font-size:14.5px;line-height:1.6;color:{Strong};">{Lines(c.Text)}</td>
                  </tr></table>
                </td>
              </tr>
            </table>
            """;
    }

    private static string Stats(EmailStats stats)
    {
        var cells = new StringBuilder();
        for (var i = 0; i < stats.Items.Count; i++)
        {
            var s = stats.Items[i];
            if (i > 0) cells.Append("""<td class="stat-gap" width="12" style="width:12px;font-size:0;line-height:0;">&nbsp;</td>""");
            cells.Append($"""
                <td class="stat soft" valign="top" bgcolor="{Soft}" style="background:{Soft};border:1px solid {Hairline};border-radius:14px;padding:16px 18px;">
                  <p class="stat-value accent-{Key(s.Tone)}" style="margin:0;font-family:{Sans};font-size:28px;line-height:1.1;font-weight:800;color:{For(s.Tone).Accent};">{E(s.Value)}</p>
                  <p class="muted" style="margin:6px 0 0;font-size:13px;line-height:1.35;color:{Muted};">{E(s.Label)}</p>
                </td>
                """);
        }

        return $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="table-layout:fixed;">
              <tr>{cells}</tr>
            </table>
            """;
    }

    private static string Section(EmailSection s)
    {
        var count = s.Count is int n
            ? $"""<span class="pill" style="display:inline-block;margin-left:8px;padding:2px 9px;border-radius:999px;background:#fef3c7;color:#92400e;font-size:12px;line-height:1.5;font-weight:700;letter-spacing:0;vertical-align:1px;">{n}</span>"""
            : "";
        return $"""<p class="strong" style="margin:30px 0 14px;font-size:13px;line-height:1.4;font-weight:800;letter-spacing:.08em;text-transform:uppercase;color:{Strong};">{E(s.Title)}{count}</p>""";
    }

    private static string Card(EmailCard card, EmailContext ctx)
    {
        var p = For(card.Tone);
        var t = Key(card.Tone);
        var inner = new StringBuilder();

        var tag = card.Tag is { Length: > 0 } tg
            ? $"""<span class="pill" style="display:inline-block;margin-left:6px;padding:1px 8px;border-radius:999px;background:#fef3c7;color:#92400e;font-size:11px;line-height:1.6;font-weight:700;vertical-align:2px;">{E(tg)}</span>"""
            : "";
        var subtitle = card.Subtitle is { Length: > 0 } st
            ? $"""<p class="muted" style="margin:3px 0 0;font-size:13.5px;line-height:1.45;color:{Muted};">{E(st)}</p>"""
            : "";
        var icon = card.Icon is { Length: > 0 } ic
            ? $"""<td width="52" valign="top" style="width:52px;"><table role="presentation" cellpadding="0" cellspacing="0" border="0"><tr><td class="tile-{t}" width="40" height="40" align="center" valign="middle" bgcolor="{p.Tile}" style="width:40px;height:40px;background:{p.Tile};border-radius:12px;font-size:20px;line-height:40px;text-align:center;">{E(ic)}</td></tr></table></td>"""
            : "";

        inner.Append($"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"><tr>
              {icon}
              <td valign="middle">
                <p class="strong" style="margin:0;font-size:16px;line-height:1.35;font-weight:700;color:{Strong};">{E(card.Title)}{tag}</p>
                {subtitle}
              </td>
            </tr></table>
            """);

        if (card.Text is { Length: > 0 } text)
            inner.Append($"""<p class="text" style="margin:12px 0 0;font-size:15px;line-height:1.6;color:{Body};">{Lines(text)}</p>""");

        if (card.Rows.Count > 0)
        {
            inner.Append("""<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:12px 0 0;">""");
            for (var i = 0; i < card.Rows.Count; i++)
            {
                var row = card.Rows[i];
                var border = i > 0 ? $"border-top:1px solid {Hairline};" : "";
                inner.Append($"""
                    <tr>
                      <td class="strong line" style="{border}padding:9px 0;font-size:15px;line-height:1.4;font-weight:600;color:{Strong};">{E(row.Name)}</td>
                      <td class="muted line" align="right" style="{border}padding:9px 0 9px 12px;font-size:14px;line-height:1.4;color:{Muted};text-align:right;">{E(row.Detail ?? "")}</td>
                    </tr>
                    """);
            }
            inner.Append("</table>");
        }

        if (card.Bullets.Count > 0) inner.Append(Bullets(card.Bullets, top: 12, bottom: 0));

        if (card.MoreHeading is { Length: > 0 } heading)
            inner.Append($"""<p class="strong" style="margin:16px 0 0;font-size:13px;line-height:1.4;font-weight:800;letter-spacing:.06em;text-transform:uppercase;color:{Strong};">{E(heading)}</p>""");

        if (card.MoreBullets.Count > 0) inner.Append(Bullets(card.MoreBullets, top: 8, bottom: 0));

        if (card.Link is { } link)
            inner.Append($"""<p style="margin:14px 0 0;font-size:14.5px;line-height:1.4;font-weight:700;"><a class="link" href="{E(Resolve(link.Url, ctx))}" target="_blank" style="color:{Link};text-decoration:none;">{E(link.Label)}&nbsp;&rarr;</a></p>""");

        var bg = card.Featured ? p.Tint : CardBg;
        var line = card.Featured ? p.TintLine : CardLine;
        var cls = card.Featured ? $"tint-{t}" : "card";
        return $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 12px;">
              <tr>
                <td class="{cls} card-pad" bgcolor="{bg}" style="background:{bg};border:1px solid {line};border-radius:16px;padding:18px 20px;">
                  {inner}
                </td>
              </tr>
            </table>
            """;
    }

    private static string Checklist(EmailChecklist list, EmailContext ctx)
    {
        var rows = new StringBuilder();
        for (var i = 0; i < list.Items.Count; i++)
        {
            var item = list.Items[i];
            var border = i > 0 ? $"border-top:1px solid {Hairline};" : "";
            var name = item.Url is { Length: > 0 } url
                ? $"""<a class="strong" href="{E(Resolve(url, ctx))}" target="_blank" style="color:{Strong};text-decoration:none;">{E(item.Name)}</a>"""
                : E(item.Name);
            var detail = item.Detail is { Length: > 0 } d
                ? $"""<p class="muted" style="margin:2px 0 0;font-size:13.5px;line-height:1.45;color:{Muted};">{E(d)}</p>"""
                : "";
            rows.Append($"""
                <tr>
                  <td class="line" width="48" valign="top" style="{border}width:48px;padding:12px 0;">
                    <table role="presentation" cellpadding="0" cellspacing="0" border="0"><tr>
                      <td class="tile-brand" width="36" height="36" align="center" valign="middle" bgcolor="#fdf1d6" style="width:36px;height:36px;background:#fdf1d6;border-radius:10px;font-size:17px;line-height:36px;text-align:center;">{E(item.Icon ?? "•")}</td>
                    </tr></table>
                  </td>
                  <td class="line" valign="middle" style="{border}padding:12px 0;">
                    <p class="strong" style="margin:0;font-size:15.5px;line-height:1.4;font-weight:600;color:{Strong};">{name}</p>
                    {detail}
                  </td>
                </tr>
                """);
        }

        return $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" class="card" bgcolor="{CardBg}" style="background:{CardBg};border:1px solid {CardLine};border-radius:16px;border-collapse:separate;">
              <tr><td class="card-pad" style="padding:6px 20px;">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">{rows}</table>
              </td></tr>
            </table>
            """;
    }

    // ── Text ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// The notification format: blank-line-separated runs become paragraphs (single line breaks inside
    /// one stay as breaks), consecutive "- " lines become a list. The last paragraph drops its bottom
    /// margin — <see cref="Gap"/> owns the space after a block.
    /// </summary>
    private static string FormatText(string message)
    {
        // Runs of paragraph lines or of "- " lines, in order.
        var runs = new List<(bool IsList, List<string> Lines)>();
        foreach (var raw in SplitLines(message))
        {
            var line = raw.Trim();
            if (line.Length == 0) { runs.Add((false, [])); continue; }

            var isList = line.StartsWith("- ");
            var content = isList ? line[2..] : line;
            if (runs.Count > 0 && runs[^1].IsList == isList && runs[^1].Lines.Count > 0) runs[^1].Lines.Add(content);
            else runs.Add((isList, [content]));
        }
        runs.RemoveAll(r => r.Lines.Count == 0);

        var html = new StringBuilder();
        for (var i = 0; i < runs.Count; i++)
        {
            var bottom = i == runs.Count - 1 ? 0 : 16;
            html.Append(runs[i].IsList
                ? Bullets(runs[i].Lines, top: 0, bottom: bottom)
                : $"""<p style="margin:0 0 {bottom}px;">{string.Join("<br>", runs[i].Lines.Select(E))}</p>""");
        }
        return html.ToString();
    }

    private static string Bullets(IEnumerable<string> items, int top, int bottom)
    {
        var rows = new StringBuilder();
        foreach (var item in items)
        {
            rows.Append($"""
                <tr>
                  <td class="dot" width="20" valign="top" style="width:20px;padding:0 0 8px;font-size:18px;line-height:24px;color:#f59e0b;">&bull;</td>
                  <td class="text" valign="top" style="padding:0 0 8px;font-size:15px;line-height:24px;color:{Body};">{E(item)}</td>
                </tr>
                """);
        }
        return $"""<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:{top}px 0 {bottom}px;">{rows}</table>""";
    }

    // ── Plain text ────────────────────────────────────────────────────────────

    /// <summary>
    /// The text/plain alternative. Clients that show it are rare, but a mail with only an HTML part
    /// scores worse with spam filters, and a screen reader in plain mode reads this.
    /// </summary>
    public static string RenderText(EmailContent c, EmailContext ctx)
    {
        var sb = new StringBuilder();
        sb.AppendLine(c.Title.ToUpperInvariant()).AppendLine();

        if (c.Greet && ctx.FirstName is { Length: > 0 } firstName)
            sb.AppendLine($"Pozdrav, {firstName}").AppendLine();

        AppendText(sb, c.Blocks, ctx);

        if (c.Button is { } button)
            sb.AppendLine($"{button.Label}: {Resolve(button.Url, ctx)}").AppendLine();

        AppendText(sb, c.After, ctx);

        sb.AppendLine("—");
        sb.AppendLine(c.Reason is { Length: > 0 } r ? r : "Ovu poruku ste primili jer imate račun u Melariumu.");
        if (c.LinkSettings) sb.AppendLine($"Postavke obavještenja: {Resolve(SettingsPath, ctx)}");
        if (!c.Internal) sb.AppendLine("Trebate pomoć? info@melarium.app");
        sb.Append("Melarium · pametno upravljanje pčelinjakom");
        return sb.ToString();
    }

    private static void AppendText(StringBuilder sb, IReadOnlyList<EmailBlock> blocks, EmailContext ctx)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case EmailText text:
                    sb.AppendLine(text.Text.Trim());
                    break;
                case EmailFacts facts:
                    foreach (var f in facts.Rows) sb.AppendLine($"{f.Label}: {FactValue(f, ctx)}");
                    break;
                case EmailCallout callout:
                    sb.AppendLine(callout.Text);
                    break;
                case EmailStats stats:
                    sb.AppendLine(string.Join(" · ", stats.Items.Select(s => $"{s.Value} {s.Label}")));
                    break;
                case EmailSection section:
                    sb.AppendLine(section.Count is int n ? $"{section.Title.ToUpperInvariant()} ({n})" : section.Title.ToUpperInvariant());
                    break;
                case EmailCard card:
                    sb.AppendLine(card.Subtitle is { Length: > 0 } s ? $"{card.Title} — {s}" : card.Title);
                    if (card.Text is { Length: > 0 } t) sb.AppendLine(t);
                    foreach (var row in card.Rows) sb.AppendLine(row.Detail is null ? $"- {row.Name}" : $"- {row.Name}: {row.Detail}");
                    foreach (var bullet in card.Bullets) sb.AppendLine($"- {bullet}");
                    if (card.MoreHeading is { Length: > 0 } h) sb.AppendLine($"{h}:");
                    foreach (var bullet in card.MoreBullets) sb.AppendLine($"- {bullet}");
                    if (card.Link is { } link) sb.AppendLine($"{link.Label}: {Resolve(link.Url, ctx)}");
                    break;
                case EmailChecklist list:
                    foreach (var item in list.Items)
                        sb.AppendLine(item.Detail is null ? $"- {item.Name}" : $"- {item.Name} ({item.Detail})");
                    break;
            }
            sb.AppendLine();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FactValue(EmailFact f, EmailContext ctx) =>
        f.SentAt ? BsLabels.LongDateTime(ctx.LocalNow) : f.Value;

    private static string Resolve(string url, EmailContext ctx) =>
        url.StartsWith('/') ? ctx.AppUrl.TrimEnd('/') + url : url;

    /// <summary>The inbox snippet: the explicit one, else the first text, flattened.</summary>
    private static string Preheader(EmailContent c)
    {
        var text = c.Preheader
            ?? c.Blocks.OfType<EmailText>().FirstOrDefault()?.Text
            ?? c.Blocks.OfType<EmailCallout>().FirstOrDefault()?.Text
            ?? c.Title;
        var flat = MultiSpace.Replace(string.Join(' ', SplitLines(text).Select(l => l.Trim().TrimStart('-').Trim())), " ").Trim();
        return flat.Length <= 140 ? flat : flat[..140].TrimEnd() + "…";
    }

    private static readonly Regex MultiSpace = new(@"\s{2,}", RegexOptions.Compiled);

    private static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    /// <summary>Escaped text with its line breaks kept.</summary>
    private static string Lines(string text) => string.Join("<br>", SplitLines(text.Trim()).Select(E));

    private static string E(string value) => WebUtility.HtmlEncode(value);
}
