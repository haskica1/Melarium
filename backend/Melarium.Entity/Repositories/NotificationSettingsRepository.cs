using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class NotificationSettingsRepository : Repository<NotificationSettings>, INotificationSettingsRepository
{
    public NotificationSettingsRepository(MelariumDbContext context) : base(context) { }

    public async Task<NotificationSettings?> GetByUserIdAsync(int userId) =>
        await _context.NotificationSettings.FirstOrDefaultAsync(s => s.UserId == userId);

    public async Task<Dictionary<int, NotificationSettings>> GetByUserIdsAsync(IReadOnlyCollection<int> userIds)
    {
        if (userIds.Count == 0) return [];

        return await _context.NotificationSettings
            .AsNoTracking()
            .Where(s => userIds.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId);
    }
}
