using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Melarium.Infrastructure.Email;

/// <summary>
/// Drains <see cref="IEmailQueue"/> and delivers notification emails outside the request path.
/// The recipient's address is resolved here (own DI scope) rather than at enqueue time so the
/// producing request does not pay for the lookup.
/// </summary>
public sealed class EmailNotificationWorker : BackgroundService
{
    private readonly IEmailQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEmailService _email;
    private readonly ILogger<EmailNotificationWorker> _logger;

    public EmailNotificationWorker(
        IEmailQueue queue,
        IServiceScopeFactory scopeFactory,
        IEmailService email,
        ILogger<EmailNotificationWorker> logger)
    {
        _queue        = queue;
        _scopeFactory = scopeFactory;
        _email        = email;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            QueuedEmail item;
            try
            {
                item = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await SendAsync(item);
            }
            catch (Exception ex)
            {
                // Email is best-effort — never let one failure kill the worker loop.
                _logger.LogError(ex, "Failed to send notification email to user {UserId}", item.UserId);
            }
        }
    }

    private async Task SendAsync(QueuedEmail item)
    {
        using var scope = _scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var user = await uow.Users.GetByIdAsync(item.UserId);
        if (user == null)
        {
            _logger.LogWarning("Notification email skipped — user {UserId} no longer exists", item.UserId);
            return;
        }

        var fullName = $"{user.FirstName} {user.LastName}";
        await _email.SendAsync(user.Email, fullName, $"Melarium — {item.Title}", BuildHtml(fullName, item));
    }

    private static string BuildHtml(string name, QueuedEmail item)
    {
        // Notification text embeds user-supplied names (hives, apiaries, organisations). Escaping
        // it keeps a hive called `<b>x` from injecting markup into everyone's inbox.
        var safeName    = Escape(name);
        var safeTitle   = Escape(item.Title);
        var safeMessage = Escape(item.Message);

        // Only ever our own absolute https/http links — never a value that reached us from a user.
        var action = item.ActionUrl is { Length: > 0 } url && item.ActionLabel is { Length: > 0 } label
            ? $"""
                <p style="margin:24px 0">
                  <a href="{Escape(url)}" style="background:#d97706;color:#fff;text-decoration:none;padding:12px 24px;border-radius:8px;display:inline-block;font-weight:600">{Escape(label)}</a>
                </p>
                <p style="color:#6b7280;font-size:12px">
                  Ako dugme ne radi, kopirajte ovaj link u pretraživač:<br>
                  <span style="color:#92400e;word-break:break-all">{Escape(url)}</span>
                </p>
              """
            : string.Empty;

        return $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:sans-serif;background:#fef9ee;padding:32px">
              <div style="max-width:520px;margin:auto;background:#fff;border-radius:12px;padding:32px;border:1px solid #f6dfa0">
                <h2 style="color:#92400e;margin-top:0">🐝 Melarium</h2>
                <p style="color:#374151">Pozdrav <strong>{safeName}</strong>,</p>
                <div style="background:#fef3c7;border-radius:8px;padding:16px;margin:16px 0">
                  <strong style="color:#92400e">{safeTitle}</strong>
                  <p style="color:#374151;margin:8px 0 0">{safeMessage}</p>
                </div>
                {action}
                <p style="color:#6b7280;font-size:12px;margin-bottom:0">
                  Ovu poruku ste primili jer imate nalog na Melarium aplikaciji.
                </p>
              </div>
            </body>
            </html>
            """;
    }

    private static string Escape(string value) => System.Net.WebUtility.HtmlEncode(value);
}
