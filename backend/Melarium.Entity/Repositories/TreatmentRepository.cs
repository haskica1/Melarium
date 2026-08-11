using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class TreatmentRepository : Repository<Treatment>, ITreatmentRepository
{
    public TreatmentRepository(MelariumDbContext context) : base(context) { }

    public async Task<IEnumerable<Treatment>> GetByOrganizationAsync(int organizationId, int? year = null) =>
        await _context.Treatments
            .AsNoTracking()
            .Include(t => t.Entries)
                .ThenInclude(e => e.Beehive)
            .Include(t => t.Rounds)
            .Include(t => t.Apiary)
            .Include(t => t.CreatedBy)
            .Where(t => t.Apiary.OrganizationId == organizationId)
            .Where(t => year == null || t.StartDate.Year == year)
            .OrderByDescending(t => t.StartDate)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Treatment>> GetByApiaryAsync(int apiaryId, int? year = null) =>
        await _context.Treatments
            .AsNoTracking()
            .Include(t => t.Entries)
                .ThenInclude(e => e.Beehive)
            .Include(t => t.Rounds)
            .Include(t => t.Apiary)
            .Include(t => t.CreatedBy)
            .Where(t => t.ApiaryId == apiaryId)
            .Where(t => year == null || t.StartDate.Year == year)
            .OrderByDescending(t => t.StartDate)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Treatment>> GetByBeehiveAsync(int beehiveId) =>
        await _context.Treatments
            .AsNoTracking()
            .Include(t => t.Entries)
                .ThenInclude(e => e.Beehive)
            .Include(t => t.Rounds)
            .Include(t => t.Apiary)
            .Include(t => t.CreatedBy)
            .Where(t => t.Entries.Any(e => e.BeehiveId == beehiveId))
            .OrderByDescending(t => t.StartDate)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Treatment>> GetByApiaryIdsAsync(IEnumerable<int> apiaryIds)
    {
        var ids = apiaryIds.ToList();
        if (ids.Count == 0) return [];

        return await _context.Treatments
            .AsNoTracking()
            .Include(t => t.Entries)
                .ThenInclude(e => e.Beehive)
            .Include(t => t.Rounds)
            .Include(t => t.Apiary)
            .Where(t => ids.Contains(t.ApiaryId))
            .OrderByDescending(t => t.StartDate)
            .ToListAsync();
    }

    public async Task<Treatment?> GetWithEntriesAsync(int id) =>
        await _context.Treatments
            .Include(t => t.Entries)
                .ThenInclude(e => e.Beehive)
            .Include(t => t.Rounds)
            .Include(t => t.Apiary)
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == id);

    public async Task<Dictionary<int, TreatmentLatestInfo>> GetLatestForBeehivesAsync(IReadOnlyCollection<int> beehiveIds)
    {
        if (beehiveIds.Count == 0) return [];

        var rows = await _context.TreatmentEntries
            .AsNoTracking()
            .Where(e => beehiveIds.Contains(e.BeehiveId))
            .Select(e => new { e.BeehiveId, T = e.Treatment })
            .ToListAsync();

        return rows
            .GroupBy(r => r.BeehiveId)
            .Select(g => g.OrderByDescending(r => r.T.StartDate).ThenByDescending(r => r.T.Id).First())
            .ToDictionary(
                r => r.BeehiveId,
                r => new TreatmentLatestInfo(
                    r.BeehiveId, r.T.Id, r.T.ProductName, r.T.ActiveSubstance, r.T.Purpose, r.T.Method,
                    r.T.StartDate, r.T.EndDate, r.T.WithdrawalDays));
    }
}
