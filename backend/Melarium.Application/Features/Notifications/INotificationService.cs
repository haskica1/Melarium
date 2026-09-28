using Melarium.Application.Common.Email;
using Melarium.Application.Features.Notifications.DTOs;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Notifications;

public interface INotificationService
{
    /// <summary>
    /// Creates an in-app notification and decides the e-mail by <see cref="INotificationPolicy"/> and
    /// the recipient's settings (SPEC-29): security types always mail, Critical mails at once, Normal
    /// scan output waits for the morning e-mail, everything else follows the user's e-mail mode.
    /// A switchable alert the user has hidden in the app is not created at all.
    /// </summary>
    /// <param name="priority">Null takes the type's default; the alert scan passes the phase's priority.</param>
    /// <param name="email">
    /// What the e-mail shows when this one is mailed at once (ADR-048) — a task card, a frost's facts.
    /// Null renders <paramref name="message"/> with the type's icon and button. The bell is unaffected.
    /// </param>
    Task NotifyAsync(
        int userId,
        string title,
        string message,
        NotificationType type,
        int? relatedEntityId = null,
        string? relatedEntityType = null,
        NotificationPriority? priority = null,
        EmailContent? email = null);

    /// <summary>
    /// Batch in-app notification for many users in one SaveChanges — deliberately no email
    /// (broadcasts like a published learning topic would be spam as individual emails).
    /// </summary>
    Task NotifyManyInAppAsync(
        IReadOnlyCollection<int> userIds,
        string title,
        string message,
        NotificationType type,
        int? relatedEntityId = null,
        string? relatedEntityType = null);

    Task<NotificationListDto> GetForUserAsync(int userId);
    Task MarkAllAsReadAsync(int userId);
    Task MarkAsReadAsync(int notificationId, int userId);

    Task<NotificationSettingsDto> GetSettingsAsync(int userId);
    Task<NotificationSettingsDto> UpdateSettingsAsync(int userId, UpdateNotificationSettingsDto dto);
}
