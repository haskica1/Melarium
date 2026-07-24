using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Notification-specific data access operations.</summary>
public interface INotificationRepository : IRepository<Notification>
{
    Task<IEnumerable<Notification>> GetByUserIdAsync(int userId);
    Task<int> GetUnreadCountAsync(int userId);
    Task MarkAllAsReadAsync(int userId);

    /// <summary>
    /// True when a notification of the same type for the same related entity was already delivered to
    /// the user since <paramref name="since"/> — the dedup guard for the alert scan (SPEC-04).
    /// </summary>
    Task<bool> ExistsRecentAsync(int userId, NotificationType type, int? relatedEntityId, DateTime since);
}
