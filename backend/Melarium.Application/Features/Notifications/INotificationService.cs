using Melarium.Application.Features.Notifications.DTOs;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Notifications;

public interface INotificationService
{
    Task NotifyAsync(
        int userId,
        string title,
        string message,
        NotificationType type,
        int? relatedEntityId = null,
        string? relatedEntityType = null);

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
}
