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
        await _email.SendAsync(user.Email, fullName, $"Melarium — {item.Title}", BuildHtml(fullName, item.Title, item.Message));
    }

    private static string BuildHtml(string name, string title, string message) => $"""
        <!DOCTYPE html>
        <html>
        <body style="font-family:sans-serif;background:#fef9ee;padding:32px">
          <div style="max-width:520px;margin:auto;background:#fff;border-radius:12px;padding:32px;border:1px solid #f6dfa0">
            <h2 style="color:#92400e;margin-top:0">🐝 Melarium</h2>
            <p style="color:#374151">Pozdrav <strong>{name}</strong>,</p>
            <div style="background:#fef3c7;border-radius:8px;padding:16px;margin:16px 0">
              <strong style="color:#92400e">{title}</strong>
              <p style="color:#374151;margin:8px 0 0">{message}</p>
            </div>
            <p style="color:#6b7280;font-size:12px;margin-bottom:0">
              Ovu poruku ste primili jer imate nalog na Melarium aplikaciji.
            </p>
          </div>
        </body>
        </html>
        """;
}
