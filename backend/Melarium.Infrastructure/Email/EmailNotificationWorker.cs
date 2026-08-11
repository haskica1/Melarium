using Melarium.Application.Common;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Models;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _config;
    private readonly ILogger<EmailNotificationWorker> _logger;

    public EmailNotificationWorker(
        IEmailQueue queue,
        IServiceScopeFactory scopeFactory,
        IEmailService email,
        IConfiguration config,
        ILogger<EmailNotificationWorker> logger)
    {
        _queue        = queue;
        _scopeFactory = scopeFactory;
        _email        = email;
        _config       = config;
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
                _logger.LogError(
                    ex,
                    "Failed to send notification email (user {UserId}, address set: {HasAddress})",
                    item.UserId,
                    item.ToEmail is { Length: > 0 });
            }
        }
    }

    private async Task SendAsync(QueuedEmail item)
    {
        // An explicit address wins: operator mail (new feedback) targets a configured destination
        // that need not correspond to a user account, so there is nothing to look up.
        if (item.ToEmail is { Length: > 0 } toEmail)
        {
            var toName = item.ToName is { Length: > 0 } n ? n : toEmail;
            await _email.SendAsync(toEmail, toName, $"Melarium — {item.Title}", RenderHtml(toName, item));
            return;
        }

        if (item.UserId is not int userId)
        {
            _logger.LogWarning("Email skipped — queued item has neither a recipient address nor a user id");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var user = await uow.Users.GetByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("Notification email skipped — user {UserId} no longer exists", userId);
            return;
        }

        var fullName = $"{user.FirstName} {user.LastName}";
        await _email.SendAsync(user.Email, fullName, $"Melarium — {item.Title}", RenderHtml(fullName, item));
    }

    private string RenderHtml(string name, QueuedEmail item) =>
        EmailTemplate.Render(
            name, item.Title, item.Message, item.ActionUrl, item.ActionLabel,
            appUrl: FrontendUrl.Build(_config, "/"));
}
