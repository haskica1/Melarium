using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class HarvestRepository : Repository<Harvest>, IHarvestRepository
{
    public HarvestRepository(MelariumDbContext context) : base(context) { }

    public async Task<IEnumerable<Harvest>> GetByOrganizationAsync(int organizationId, HarvestKind kind, int? year = null) =>
        await OfKind(_context.Harvests.AsNoTracking(), kind)
            .Include(h => h.Entries)
            .Include(h => h.Apiary)
            .Include(h => h.CreatedBy)
            .Where(h => h.OrganizationId == organizationId)
            .Where(h => year == null || h.Date.Year == year)
            .OrderByDescending(h => h.Date)
            .ThenByDescending(h => h.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Harvest>> GetByApiaryAsync(int apiaryId, HarvestKind kind, int? year = null) =>
        await OfKind(_context.Harvests.AsNoTracking(), kind)
            .Include(h => h.Entries)
            .Include(h => h.Apiary)
            .Include(h => h.CreatedBy)
            .Where(h => h.ApiaryId == apiaryId)
            .Where(h => year == null || h.Date.Year == year)
            .OrderByDescending(h => h.Date)
            .ThenByDescending(h => h.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Harvest>> GetByApiariesAsync(IReadOnlyCollection<int> apiaryIds, HarvestKind kind, int? year = null)
    {
        if (apiaryIds.Count == 0) return [];

        return await OfKind(_context.Harvests.AsNoTracking(), kind)
            .Include(h => h.Entries)
            .Include(h => h.Apiary)
            .Where(h => h.ApiaryId != null && apiaryIds.Contains(h.ApiaryId.Value))
            .Where(h => year == null || h.Date.Year == year)
            .OrderByDescending(h => h.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Harvest>> GetSharedAsync(int? organizationId, HarvestKind kind, int? year = null) =>
        await OfKind(_context.Harvests.AsNoTracking(), kind)
            .Include(h => h.Entries)
            .Include(h => h.CreatedBy)
            .Where(h => h.ApiaryId == null)
            .Where(h => organizationId == null || h.OrganizationId == organizationId)
            .Where(h => year == null || h.Date.Year == year)
            .OrderByDescending(h => h.Date)
            .ThenByDescending(h => h.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Harvest>> GetByBeehiveAsync(int beehiveId, HarvestKind kind) =>
        await OfKind(_context.Harvests.AsNoTracking(), kind)
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

    public async Task<Dictionary<int, decimal>> GetHiveTotalsAsync(IReadOnlyCollection<int> beehiveIds, HarvestKind kind, int? year = null)
    {
        if (beehiveIds.Count == 0) return [];

        var entries = _context.HarvestEntries.AsNoTracking().Where(e => beehiveIds.Contains(e.BeehiveId));
        entries = kind switch
        {
            HarvestKind.Honey         => entries.Where(e => e.Harvest.ProductType == HiveProductType.Honey),
            HarvestKind.OtherProducts => entries.Where(e => e.Harvest.ProductType != HiveProductType.Honey),
            _                         => entries,
        };

        return await entries
            .Where(e => year == null || e.Harvest.Date.Year == year)
            .GroupBy(e => e.BeehiveId)
            .Select(g => new { BeehiveId = g.Key, TotalKg = g.Sum(e => e.QuantityKg) })
            .ToDictionaryAsync(x => x.BeehiveId, x => x.TotalKg);
    }

    public async Task<Dictionary<(int Year, HiveProductType ProductType), decimal>> GetHiveTotalsByYearAsync(int beehiveId)
    {
        var rows = await _context.HarvestEntries
            .AsNoTracking()
            .Where(e => e.BeehiveId == beehiveId)
            .GroupBy(e => new { e.Harvest.Date.Year, e.Harvest.ProductType })
            .Select(g => new { g.Key.Year, g.Key.ProductType, Kg = g.Sum(e => e.QuantityKg) })
            .ToListAsync();

        return rows.ToDictionary(r => (r.Year, r.ProductType), r => r.Kg);
    }

    private static IQueryable<Harvest> OfKind(IQueryable<Harvest> query, HarvestKind kind) => kind switch
    {
        HarvestKind.Honey         => query.Where(h => h.ProductType == HiveProductType.Honey),
        HarvestKind.OtherProducts => query.Where(h => h.ProductType != HiveProductType.Honey),
        _                         => query,
    };
}
