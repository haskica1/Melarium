namespace Melarium.Application.Features.Notifications.DTOs;

public record NotificationDto(
    int Id,
    string Title,
    string Message,
    string Type,
    bool IsRead,
    DateTime CreatedAt,
    int? RelatedEntityId,
    string? RelatedEntityType,
    // Same string form as Type. Stored per row, because frost is Critical in April and Normal in August.
    string Priority = "Normal"
);
