using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Per-user notification preferences (SPEC-29).</summary>
public interface INotificationSettingsRepository : IRepository<NotificationSettings>
{
    /// <summary>The user's settings row, tracked for edits; null if never saved.</summary>
    Task<NotificationSettings?> GetByUserIdAsync(int userId);

    /// <summary>Settings of the given users, read-only, keyed by user id — users without a row are absent.</summary>
    Task<Dictionary<int, NotificationSettings>> GetByUserIdsAsync(IReadOnlyCollection<int> userIds);
}
