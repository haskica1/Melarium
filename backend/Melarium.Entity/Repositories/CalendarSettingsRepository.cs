using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class CalendarSettingsRepository : Repository<CalendarSettings>, ICalendarSettingsRepository
{
    public CalendarSettingsRepository(MelariumDbContext context) : base(context) { }

    // Tracked — callers mutate the returned row (token + toggles) and save.
    public async Task<CalendarSettings?> GetByUserIdAsync(int userId) =>
        await _context.CalendarSettings.FirstOrDefaultAsync(s => s.UserId == userId);

    public async Task<CalendarSettings?> GetByFeedTokenAsync(string token) =>
        await _context.CalendarSettings.AsNoTracking().FirstOrDefaultAsync(s => s.FeedToken == token);
}
