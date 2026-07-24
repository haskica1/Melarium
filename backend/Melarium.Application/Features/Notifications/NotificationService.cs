using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Models;
using Melarium.Application.Features.Notifications.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Notifications;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _uow;
    private readonly IEmailQueue _emailQueue;

    public NotificationService(IUnitOfWork uow, IEmailQueue emailQueue)
    {
        _uow        = uow;
        _emailQueue = emailQueue;
    }

    public async Task NotifyAsync(
        int userId,
        string title,
        string message,
        NotificationType type,
        int? relatedEntityId = null,
        string? relatedEntityType = null)
    {
        var notification = new Notification
        {
            UserId            = userId,
            Title             = title,
            Message           = message,
            Type              = type,
            RelatedEntityId   = relatedEntityId,
            RelatedEntityType = relatedEntityType,
        };

        await _uow.Notifications.AddAsync(notification);
        await _uow.SaveChangesAsync();

        // Email goes through the background worker so SMTP latency/failures never
        // affect the request that produced the notification.
        _emailQueue.Enqueue(new QueuedEmail(userId, title, message));
    }

    public async Task NotifyManyInAppAsync(
        IReadOnlyCollection<int> userIds,
        string title,
        string message,
        NotificationType type,
        int? relatedEntityId = null,
        string? relatedEntityType = null)
    {
        foreach (var userId in userIds.Distinct())
        {
            await _uow.Notifications.AddAsync(new Notification
            {
                UserId            = userId,
                Title             = title,
                Message           = message,
                Type              = type,
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
            n.RelatedEntityType));

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
}
