using System.Text;

namespace Melarium.Infrastructure.Email;

/// <summary>
/// Renders the single HTML shell every outbound e-mail uses — notifications, password reset/verify,
/// operator feedback alerts, the SMTP test ping. One template means the honey/hive brand (see the
/// frontend's <c>tailwind.config.js</c> and <c>AuthCard.tsx</c>, which this mirrors) lives in one
/// place instead of drifting per call site. Table-based layout + inlined styles so it survives
/// Outlook's Word rendering engine; a `prefers-color-scheme` block and fluid widths cover dark mode
/// and small screens for clients that support them.
/// </summary>
public static class EmailTemplate
{
    public static string Render(
        string name,
        string title,
        string message,
        string? actionUrl = null,
        string? actionLabel = null,
        string? appUrl = null)
    {
        var safeName  = Escape(name);
        var safeTitle = Escape(title);
        var preheader = Escape(BuildPreheader(message));
        var body      = FormatMessage(message);
        var action    = actionUrl is { Length: > 0 } url && actionLabel is { Length: > 0 } label
            ? BuildAction(url, label)
            : string.Empty;

        var brand = appUrl is { Length: > 0 } home
            ? $"""<a class="brand-link" href="{Escape(home)}" target="_blank" style="color:#b45309;font-weight:600;text-decoration:none;">🐝 Melarium</a>"""
            : """<span class="brand-link" style="color:#b45309;font-weight:600;">🐝 Melarium</span>""";

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
            <title>{{safeTitle}}</title>
            <!--[if mso]>
            <noscript>
              <xml>
                <o:OfficeDocumentSettings>
                  <o:PixelsPerInch>96</o:PixelsPerInch>
                </o:OfficeDocumentSettings>
              </xml>
            </noscript>
            <![endif]-->
            <style>
              @import url('https://fonts.googleapis.com/css2?family=Playfair+Display:wght@700&family=DM+Sans:wght@400;500;700&display=swap');
              body, table, td, a { -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; }
              table, td { mso-table-lspace: 0pt; mso-table-rspace: 0pt; }
              img { -ms-interpolation-mode: bicubic; border: 0; outline: none; text-decoration: none; }
              body { margin: 0; padding: 0; width: 100% !important; background: #fffbeb; }
              a { text-decoration: none; }

              @media (max-width: 600px) {
                .container { width: 100% !important; }
                .card { padding: 28px 22px !important; border-radius: 16px !important; }
                .title { font-size: 20px !important; }
                .wordmark { font-size: 22px !important; }
                .btn-cell a { display: block !important; padding: 14px 18px !important; }
              }

              @media (prefers-color-scheme: dark) {
                body, .bg-body { background: #020617 !important; }
                .card { background: #0f172a !important; border-color: #1e293b !important; }
                .wordmark, .title { color: #fcd34d !important; }
                .text-body { color: #e2e8f0 !important; }
                .text-muted { color: #94a3b8 !important; }
                .bullet { color: #fbbf24 !important; }
                .link-chip { background: #1e293b !important; color: #fcd34d !important; }
                .brand-link { color: #fbbf24 !important; }
              }
            </style>
            </head>
            <body class="bg-body" style="margin:0;padding:0;background:#fffbeb;">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0;mso-hide:all;font-size:1px;line-height:1px;color:#fffbeb;">
                {{preheader}}
                &#8203;&zwnj;&nbsp;&#8203;&zwnj;&nbsp;&#8203;&zwnj;&nbsp;&#8203;&zwnj;&nbsp;&#8203;&zwnj;&nbsp;&#8203;&zwnj;&nbsp;&#8203;&zwnj;&nbsp;&#8203;&zwnj;&nbsp;
              </div>

              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" bgcolor="#fffbeb" class="bg-body" style="background:#fffbeb;">
                <tr>
                  <td align="center" style="padding:40px 16px;">

                    <table role="presentation" width="600" cellpadding="0" cellspacing="0" border="0" class="container" style="width:600px;max-width:100%;">

                      <tr>
                        <td align="center" style="padding-bottom:26px;">
                          <div style="font-size:38px;line-height:1;">🐝</div>
                          <div class="wordmark" style="font-family:'Playfair Display',Georgia,serif;font-weight:700;font-size:25px;color:#92400e;letter-spacing:.2px;padding-top:4px;">
                            Melarium
                          </div>
                        </td>
                      </tr>

                      <tr>
                        <td class="card" bgcolor="#ffffff" style="background:#ffffff;border:1px solid #f6dfa0;border-top:4px solid #f59e0b;border-radius:20px;padding:40px;">

                          <p class="text-muted" style="margin:0 0 6px;color:#6b7280;font-family:'DM Sans',Arial,sans-serif;font-size:13px;">
                            Pozdrav, {{safeName}}
                          </p>

                          <h1 class="title" style="margin:0 0 16px;color:#92400e;font-family:'Playfair Display',Georgia,serif;font-weight:700;font-size:22px;line-height:1.35;">
                            {{safeTitle}}
                          </h1>

                          <div class="text-body" style="color:#374151;font-family:'DM Sans',Arial,sans-serif;font-size:15px;line-height:1.65;">
                            {{body}}
                          </div>

                          {{action}}

                        </td>
                      </tr>

                      <tr>
                        <td align="center" style="padding:26px 20px 0;">
                          <p class="text-muted" style="margin:0 0 8px;color:#6b7280;font-family:'DM Sans',Arial,sans-serif;font-size:12.5px;line-height:1.6;">
                            Ovu poruku ste primili jer imate nalog na Melarium aplikaciji.
                          </p>
                          <p style="margin:0;font-family:'DM Sans',Arial,sans-serif;font-size:12.5px;">
                            {{brand}}
                            <span class="text-muted" style="color:#6b7280;"> · pametno upravljanje pčelinjakom</span>
                          </p>
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

    private static string BuildAction(string url, string label)
    {
        var safeUrl   = Escape(url);
        var safeLabel = Escape(label);

        return $$"""
            <table role="presentation" cellpadding="0" cellspacing="0" border="0" align="center" style="margin:28px auto 0;">
              <tr>
                <td class="btn-cell" align="center" bgcolor="#d97706" style="border-radius:10px;background:#d97706;background-image:linear-gradient(135deg,#f59e0b,#d97706);">
                  <a href="{{safeUrl}}" target="_blank" style="display:inline-block;padding:14px 34px;font-family:'DM Sans',Arial,sans-serif;font-size:15px;font-weight:700;color:#ffffff;text-decoration:none;border-radius:10px;">
                    {{safeLabel}}
                  </a>
                </td>
              </tr>
            </table>
            <p class="text-muted" style="margin:16px 0 0;text-align:center;color:#6b7280;font-family:'DM Sans',Arial,sans-serif;font-size:12px;line-height:1.6;">
              Ako dugme ne radi, kopirajte ovaj link u pretraživač:<br>
              <span class="link-chip" style="display:inline-block;margin-top:6px;padding:6px 10px;background:#fef3c7;color:#92400e;border-radius:6px;font-size:11.5px;word-break:break-all;">{{safeUrl}}</span>
            </p>
            """;
    }

    /// <summary>
    /// Turns a plain-text notification message into HTML: blank-line-separated runs become
    /// paragraphs (single "\n"s inside one become soft line breaks), and consecutive lines starting
    /// with "- " become a bulleted list. Covers everything the app currently queues — a one-line
    /// notification, the multi-paragraph feedback mail (subject/sender/page, blank line, free-text
    /// body — see <c>FeedbackService</c>), and the AI weekly digest's "- " bullet lines (see
    /// <c>WeeklySummaryService</c>) — without the caller having to know which shape it's sending.
    /// </summary>
    private static string FormatMessage(string message)
    {
        var lines     = message.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var html      = new StringBuilder();
        var paragraph = new List<string>();
        var bullets   = new List<string>();

        void FlushParagraph()
        {
            if (paragraph.Count == 0) return;
            html.Append("<p style=\"margin:0 0 14px;\">")
                .Append(string.Join("<br>", paragraph.Select(Escape)))
                .Append("</p>");
            paragraph.Clear();
        }

        void FlushBullets()
        {
            if (bullets.Count == 0) return;
            html.Append("<ul style=\"list-style:none;margin:0 0 14px;padding:0;\">");
            foreach (var item in bullets)
            {
                html.Append("<li style=\"margin:0 0 8px;padding-left:20px;text-indent:-20px;\">")
                    .Append("<span class=\"bullet\" style=\"color:#d97706;font-weight:700;\">&bull;</span>&nbsp; ")
                    .Append(Escape(item))
                    .Append("</li>");
            }
            html.Append("</ul>");
            bullets.Clear();
        }

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) { FlushParagraph(); FlushBullets(); continue; }

            if (line.StartsWith("- "))
            {
                FlushParagraph();
                bullets.Add(line[2..]);
            }
            else
            {
                FlushBullets();
                paragraph.Add(line);
            }
        }
        FlushParagraph();
        FlushBullets();

        return html.Length > 0 ? html.ToString() : $"<p style=\"margin:0;\">{Escape(message)}</p>";
    }

    /// <summary>Flattens the message to one line for the inbox preview snippet, stripped of bullet dashes.</summary>
    private static string BuildPreheader(string message)
    {
        var flat = message.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Replace("- ", "");
        while (flat.Contains("  ")) flat = flat.Replace("  ", " ");
        flat = flat.Trim();
        return flat.Length <= 140 ? flat : flat[..140].TrimEnd() + "…";
    }

    private static string Escape(string value) => System.Net.WebUtility.HtmlEncode(value);
}
