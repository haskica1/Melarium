namespace Melarium.Application.Features.Notifications.DTOs;

public record NotificationListDto(
    IEnumerable<NotificationDto> Notifications,
    int UnreadCount
);
