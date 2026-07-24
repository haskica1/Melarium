using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class HarvestRepository : Repository<Harvest>, IHarvestRepository
{
    public HarvestRepository(MelariumDbContext context) : base(context) { }

    public async Task<IEnumerable<Harvest>> GetByOrganizationAsync(int organizationId, int? year = null) =>
        await _context.Harvests
            .AsNoTracking()
            .Include(h => h.Entries)
            .Include(h => h.Apiary)
            .Include(h => h.CreatedBy)
            .Where(h => h.Apiary.OrganizationId == organizationId)
            .Where(h => year == null || h.Date.Year == year)
            .OrderByDescending(h => h.Date)
            .ThenByDescending(h => h.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Harvest>> GetByApiaryAsync(int apiaryId, int? year = null) =>
        await _context.Harvests
            .AsNoTracking()
            .Include(h => h.Entries)
            .Include(h => h.Apiary)
            .Include(h => h.CreatedBy)
            .Where(h => h.ApiaryId == apiaryId)
            .Where(h => year == null || h.Date.Year == year)
            .OrderByDescending(h => h.Date)
            .ThenByDescending(h => h.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Harvest>> GetByApiariesAsync(IReadOnlyCollection<int> apiaryIds, int? year = null)
    {
        if (apiaryIds.Count == 0) return [];

        return await _context.Harvests
            .AsNoTracking()
            .Include(h => h.Entries)
            .Include(h => h.Apiary)
            .Where(h => apiaryIds.Contains(h.ApiaryId))
            .Where(h => year == null || h.Date.Year == year)
            .OrderByDescending(h => h.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Harvest>> GetByBeehiveAsync(int beehiveId) =>
        await _context.Harvests
            .AsNoTracking()
            .Include(h => h.Entries)
            .Include(h => h.Apiary)
            .Include(h => h.CreatedBy)
            .Where(h => h.Entries.Any(e => e.BeehiveId == beehiveId))
            .OrderByDescending(h => h.Date)
            .ThenByDescending(h => h.CreatedAt)
            .ToListAsync();

    public async Task<Harvest?> GetWithEntriesAsync(int id) =>
        await _context.Harvests
            .Include(h => h.Entries)
                .ThenInclude(e => e.Beehive)
            .Include(h => h.Apiary)
            .Include(h => h.CreatedBy)
            .FirstOrDefaultAsync(h => h.Id == id);

    public async Task<Dictionary<int, decimal>> GetHiveTotalsAsync(IReadOnlyCollection<int> beehiveIds, int? year = null)
    {
        if (beehiveIds.Count == 0) return [];

        return await _context.HarvestEntries
            .AsNoTracking()
            .Where(e => beehiveIds.Contains(e.BeehiveId))
            .Where(e => year == null || e.Harvest.Date.Year == year)
            .GroupBy(e => e.BeehiveId)
            .Select(g => new { BeehiveId = g.Key, TotalKg = g.Sum(e => e.QuantityKg) })
            .ToDictionaryAsync(x => x.BeehiveId, x => x.TotalKg);
    }

    public async Task<Dictionary<int, decimal>> GetHiveYearlyTotalsAsync(int beehiveId) =>
        await _context.HarvestEntries
            .AsNoTracking()
            .Where(e => e.BeehiveId == beehiveId)
            .GroupBy(e => e.Harvest.Date.Year)
            .Select(g => new { Year = g.Key, Kg = g.Sum(e => e.QuantityKg) })
            .ToDictionaryAsync(x => x.Year, x => x.Kg);
}
