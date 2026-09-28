namespace Melarium.Application.Common.Interfaces;

public interface IEmailService
{
    /// <summary>Sends best-effort: an unconfigured or failing SMTP is logged, never thrown.</summary>
    /// <param name="textBody">The text/plain alternative; the mail goes out HTML-only without it.</param>
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody, string? textBody = null);

    /// <summary>Same, but <paramref name="suppressErrors"/> false re-throws — for the SMTP test endpoint.</summary>
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody, bool suppressErrors, string? textBody = null);
}
