using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class InspectionRepository : Repository<Inspection>, IInspectionRepository
{
    public InspectionRepository(MelariumDbContext context) : base(context) { }

    public async Task<IEnumerable<Inspection>> GetByBeehiveIdAsync(int beehiveId) =>
        await _context.Inspections
            .AsNoTracking()
            .Where(i => i.BeehiveId == beehiveId)
            .OrderByDescending(i => i.Date)
            .ToListAsync();

    public async Task<Dictionary<int, int>> CountByBeehiveForApiaryAsync(int apiaryId) =>
        await _context.Inspections
            .Where(i => i.Beehive.ApiaryId == apiaryId)
            .GroupBy(i => i.BeehiveId)
            .Select(g => new { BeehiveId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BeehiveId, x => x.Count);

    public async Task<Dictionary<int, DateTime>> GetLastDatesAsync(IReadOnlyCollection<int> beehiveIds)
    {
        if (beehiveIds.Count == 0) return [];
        var ids = beehiveIds.ToList();

        return await _context.Inspections
            .Where(i => ids.Contains(i.BeehiveId))
            .GroupBy(i => i.BeehiveId)
            .Select(g => new { BeehiveId = g.Key, Last = g.Max(i => i.Date) })
            .ToDictionaryAsync(x => x.BeehiveId, x => x.Last);
    }

    public async Task<List<InspectionLevelInfo>> GetLevelsSinceAsync(IReadOnlyCollection<int> beehiveIds, DateTime since)
    {
        if (beehiveIds.Count == 0) return [];
        var ids = beehiveIds.ToList();

        return await _context.Inspections
            .AsNoTracking()
            .Where(i => ids.Contains(i.BeehiveId) && i.Date >= since)
            .OrderByDescending(i => i.Date)
            .Select(i => new InspectionLevelInfo(i.BeehiveId, i.Date, i.HoneyLevel))
            .ToListAsync();
    }
}
