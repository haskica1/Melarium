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

    /// <summary>
    /// Normal-priority notifications of the given types created since <paramref name="since"/>, for every
    /// user at once — the alert half of the morning e-mail (SPEC-29), read once per run, not per user.
    /// </summary>
    Task<List<Notification>> GetNormalSinceAsync(DateTime since, IReadOnlyCollection<NotificationType> types);
}
