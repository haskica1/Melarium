using Melarium.Application.Common.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Melarium.Infrastructure.Email;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        if (!IsConfigured())
        {
            _logger.LogDebug("SMTP not configured — skipping email to {Email}", toEmail);
            return;
        }

        await SendCoreAsync(toEmail, toName, subject, htmlBody, suppressErrors: true);
    }

    /// <summary>Sends email and optionally re-throws on failure (used by the test endpoint).</summary>
    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, bool suppressErrors)
    {
        if (!IsConfigured())
            throw new InvalidOperationException(
                "SMTP is not configured. Set Smtp:Host and Smtp:Password (environment variables in production).");

        await SendCoreAsync(toEmail, toName, subject, htmlBody, suppressErrors);
    }

    private bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(_config["Smtp:Host"]) &&
        !string.IsNullOrWhiteSpace(_config["Smtp:Password"]);

    private async Task SendCoreAsync(string toEmail, string toName, string subject, string htmlBody, bool suppressErrors)
    {
        var host      = _config["Smtp:Host"]!;
        var port      = int.Parse(_config["Smtp:Port"] ?? "587");
        var username  = _config["Smtp:Username"] ?? string.Empty;
        var password  = _config["Smtp:Password"] ?? string.Empty;
        var fromEmail = _config["Smtp:FromEmail"] ?? "noreply@melarium.app";
        var fromName  = _config["Smtp:FromName"] ?? "Melarium";

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent to {Email} — subject: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email} — subject: {Subject}", toEmail, subject);
            if (!suppressErrors) throw;
        }
    }
}
