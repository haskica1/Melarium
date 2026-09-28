using Melarium.Application.Common.Email;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Models;
using Melarium.Application.Features.Notifications.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Notifications;

public class NotificationService : INotificationService
{
    /// <summary>The column is varchar(1000); a longer message would fail the whole save, not just itself.</summary>
    public const int MaxMessageLength = 1000;

    private readonly IUnitOfWork _uow;
    private readonly IEmailQueue _emailQueue;
    private readonly INotificationPolicy _policy;

    // The alert scan notifies hundreds of users in one scope; one settings read per user is enough.
    private readonly Dictionary<int, NotificationSettings?> _settingsCache = [];

    public NotificationService(IUnitOfWork uow, IEmailQueue emailQueue, INotificationPolicy policy)
    {
        _uow        = uow;
        _emailQueue = emailQueue;
        _policy     = policy;
    }

    public async Task NotifyAsync(
        int userId,
        string title,
        string message,
        NotificationType type,
        int? relatedEntityId = null,
        string? relatedEntityType = null,
        NotificationPriority? priority = null,
        EmailContent? email = null)
    {
        var effective = priority ?? _policy.PriorityOf(type);
        var settings = await SettingsForAsync(userId);

        if (!_policy.ShowInApp(type, effective, settings)) return;

        message = Fit(message);
        var notification = new Notification
        {
            UserId            = userId,
            Title             = title,
            Message           = message,
            Type              = type,
            Priority          = effective,
            RelatedEntityId   = relatedEntityId,
            RelatedEntityType = relatedEntityType,
        };

        await _uow.Notifications.AddAsync(notification);
        await _uow.SaveChangesAsync();

        // Email goes through the background worker so SMTP latency/failures never
        // affect the request that produced the notification.
        if (_policy.ShouldEmailNow(type, effective, settings?.EmailMode ?? EmailNotificationMode.All))
        {
            _emailQueue.Enqueue(QueuedEmail.ForUser(userId, NotificationEmail.Compose(
                type, effective, title, message, relatedEntityId, relatedEntityType, email, _policy.IsSecurity(type))));
        }
    }

    public async Task NotifyManyInAppAsync(
        IReadOnlyCollection<int> userIds,
        string title,
        string message,
        NotificationType type,
        int? relatedEntityId = null,
        string? relatedEntityType = null)
    {
        var priority = _policy.PriorityOf(type);
        message = Fit(message);

        foreach (var userId in userIds.Distinct())
        {
            await _uow.Notifications.AddAsync(new Notification
            {
                UserId            = userId,
                Title             = title,
                Message           = message,
                Type              = type,
                Priority          = priority,
                RelatedEntityId   = relatedEntityId,
                RelatedEntityType = relatedEntityType,
            });
        }

        await _uow.SaveChangesAsync();
    }

    public async Task<NotificationListDto> GetForUserAsync(int userId)
    {
        var notifications = await _uow.Notifications.GetByUserIdAsync(userId);
        var unreadCount   = await _uow.Notifications.GetUnreadCountAsync(userId);
        var dtos = notifications.Select(n => new NotificationDto(
            n.Id,
            n.Title,
            n.Message,
            n.Type.ToString(),
            n.IsRead,
            n.CreatedAt,
            n.RelatedEntityId,
            n.RelatedEntityType,
            n.Priority.ToString()));

        return new NotificationListDto(dtos, unreadCount);
    }

    public async Task MarkAllAsReadAsync(int userId) =>
        await _uow.Notifications.MarkAllAsReadAsync(userId);

    public async Task MarkAsReadAsync(int notificationId, int userId)
    {
        var notification = await _uow.Notifications.GetByIdAsync(notificationId);
        if (notification == null || notification.UserId != userId) return;

        notification.IsRead = true;
        await _uow.Notifications.UpdateAsync(notification);
        await _uow.SaveChangesAsync();
    }

    // ── Settings (SPEC-29) ─────────────────────────────────────────────────────

    public async Task<NotificationSettingsDto> GetSettingsAsync(int userId) =>
        ToDto(await _uow.NotificationSettings.GetByUserIdAsync(userId) ?? new NotificationSettings());

    public async Task<NotificationSettingsDto> UpdateSettingsAsync(int userId, UpdateNotificationSettingsDto dto)
    {
        var settings = await _uow.NotificationSettings.GetByUserIdAsync(userId);
        var isNew = settings is null;
        settings ??= new NotificationSettings { UserId = userId };

        settings.EmailMode         = dto.EmailMode;
        settings.NormalAlertsInApp = dto.NormalAlertsInApp;
        settings.InfoAlertsInApp   = dto.InfoAlertsInApp;

        if (isNew) await _uow.NotificationSettings.AddAsync(settings);
        else await _uow.NotificationSettings.UpdateAsync(settings);
        await _uow.SaveChangesAsync();
        _settingsCache.Remove(userId);

        return ToDto(settings);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<NotificationSettings?> SettingsForAsync(int userId)
    {
        if (_settingsCache.TryGetValue(userId, out var cached)) return cached;

        var settings = await _uow.NotificationSettings.GetByUserIdAsync(userId);
        _settingsCache[userId] = settings;
        return settings;
    }

    private static NotificationSettingsDto ToDto(NotificationSettings s) =>
        new(s.EmailMode, s.NormalAlertsInApp, s.InfoAlertsInApp);

    /// <summary>Cuts an over-long message at a line break where possible, and says it was cut.</summary>
    public static string Fit(string message)
    {
        if (message.Length <= MaxMessageLength) return message;

        const string more = "\n…";
        var cut = message[..(MaxMessageLength - more.Length)];
        var lastBreak = cut.LastIndexOf('\n');
        if (lastBreak > MaxMessageLength / 2) cut = cut[..lastBreak];
        return cut.TrimEnd() + more;
    }
}
