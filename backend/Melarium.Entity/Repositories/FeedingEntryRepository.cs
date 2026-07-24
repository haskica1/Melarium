using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class FeedingEntryRepository : Repository<FeedingEntry>, IFeedingEntryRepository
{
    public FeedingEntryRepository(MelariumDbContext context) : base(context) { }

    public async Task<IEnumerable<FeedingEntry>> GetByDietIdAsync(int dietId) =>
        await _context.FeedingEntries
            .AsNoTracking()
            .Where(e => e.DietId == dietId)
            .OrderBy(e => e.ScheduledDate)
            .ToListAsync();
}
