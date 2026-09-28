using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

public class Notification : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }

    /// <summary>
    /// Stored, not derived from <see cref="Type"/>: the same type can be Critical in April and Normal in
    /// August (frost, SPEC-29), and the morning e-mail has to know which one a row was when it was made.
    /// </summary>
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

    public bool IsRead { get; set; } = false;

    public int? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
}
