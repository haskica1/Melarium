using Melarium.Application.Common.Interfaces;
using Melarium.Infrastructure.Alerts;
using Melarium.Infrastructure.Email;
using Melarium.Infrastructure.Reminders;
using Melarium.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Melarium.Infrastructure;

/// <summary>
/// Registers Infrastructure-layer services — external integrations such as email delivery.
/// Persistence lives in the <c>Melarium.Entity</c> project (see <c>AddEntity</c>).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IEmailService, EmailService>();

        // Blob storage for uploaded files (SPEC-05): local disk in dev, S3-compatible in prod.
        // Switching providers is config-only — no code changes (Storage:Provider = Local | S3).
        var storageProvider = configuration["Storage:Provider"] ?? "Local";
        if (storageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IFileStorage, S3FileStorage>();
        else
            services.AddSingleton<IFileStorage, LocalDiskFileStorage>();

        // Notification emails are delivered by a background worker so SMTP never
        // blocks (or times out) the HTTP request that produced the notification.
        services.AddSingleton<IEmailQueue, ChannelEmailQueue>();
        services.AddHostedService<EmailNotificationWorker>();

        // Daily proactive alert scan + weekly AI summary (SPEC-04).
        services.AddHostedService<AlertScanWorker>();

        // Daily 08:00-local obligation agenda (SPEC-11 Faza A.2).
        services.AddHostedService<DailyAgendaWorker>();

        return services;
    }
}
