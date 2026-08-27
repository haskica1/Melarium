using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class BeehiveMergeRepository : Repository<BeehiveMerge>, IBeehiveMergeRepository
{
    public BeehiveMergeRepository(MelariumDbContext context) : base(context) { }

    public async Task<BeehiveMerge?> GetWithHivesAsync(int id) =>
        await _context.BeehiveMerges
            .Include(m => m.SourceBeehive)
            .Include(m => m.TargetBeehive)
            .Include(m => m.CreatedBy)
            .FirstOrDefaultAsync(m => m.Id == id);

    public async Task<IEnumerable<BeehiveMerge>> GetReceivedByBeehiveAsync(int beehiveId) =>
        await _context.BeehiveMerges
            .AsNoTracking()
            .Include(m => m.SourceBeehive)
            .Include(m => m.TargetBeehive)
            .Include(m => m.CreatedBy)
            .Where(m => m.TargetBeehiveId == beehiveId && m.UndoneAt == null)
            .OrderByDescending(m => m.MergedAt)
            .ThenByDescending(m => m.Id)
            .ToListAsync();

    public async Task<BeehiveMerge?> GetActiveBySourceAsync(int sourceBeehiveId) =>
        await _context.BeehiveMerges
            .Include(m => m.SourceBeehive)
            .Include(m => m.TargetBeehive)
            .Include(m => m.CreatedBy)
            .FirstOrDefaultAsync(m => m.SourceBeehiveId == sourceBeehiveId && m.UndoneAt == null);

    public async Task<IEnumerable<BeehiveMerge>> GetReceivedByBeehivesAsync(IReadOnlyCollection<int> beehiveIds) =>
        await _context.BeehiveMerges
            .AsNoTracking()
            .Include(m => m.SourceBeehive)
            .Where(m => beehiveIds.Contains(m.TargetBeehiveId) && m.UndoneAt == null)
            .OrderByDescending(m => m.MergedAt)
            .ToListAsync();
}
