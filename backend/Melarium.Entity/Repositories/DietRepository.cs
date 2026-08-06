using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class DietRepository : Repository<Diet>, IDietRepository
{
    public DietRepository(MelariumDbContext context) : base(context) { }

    public async Task<IEnumerable<Diet>> GetByApiaryAsync(int apiaryId, int? year = null) =>
        await _context.Diets
            .AsNoTracking()
            .Include(d => d.FeedingEntries)
            .Include(d => d.Beehives)
            .Include(d => d.Apiary)
            .Include(d => d.CreatedBy)
            .Where(d => d.ApiaryId == apiaryId)
            .Where(d => year == null || d.StartDate.Year == year)
            .OrderByDescending(d => d.StartDate)
            .ThenByDescending(d => d.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Diet>> GetByOrganizationAsync(int organizationId, int? year = null) =>
        await _context.Diets
            .AsNoTracking()
            .Include(d => d.FeedingEntries)
            .Include(d => d.Beehives)
            .Include(d => d.Apiary)
            .Include(d => d.CreatedBy)
            .Where(d => d.Apiary.OrganizationId == organizationId)
            .Where(d => year == null || d.StartDate.Year == year)
            .OrderByDescending(d => d.StartDate)
            .ThenByDescending(d => d.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Diet>> GetByBeehiveAsync(int beehiveId) =>
        await _context.Diets
            .AsNoTracking()
            .Include(d => d.FeedingEntries)
            .Include(d => d.Beehives)
            .Include(d => d.Apiary)
            .Include(d => d.CreatedBy)
            .Where(d => d.Beehives.Any(db => db.BeehiveId == beehiveId))
            .OrderByDescending(d => d.StartDate)
            .ThenByDescending(d => d.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Diet>> GetByApiaryIdsAsync(IEnumerable<int> apiaryIds)
    {
        var ids = apiaryIds.ToList();
        if (ids.Count == 0) return [];

        return await _context.Diets
            .AsNoTracking()
            .Include(d => d.FeedingEntries)
            .Include(d => d.Beehives)
            .Where(d => ids.Contains(d.ApiaryId))
            .OrderByDescending(d => d.StartDate)
            .ToListAsync();
    }

    public async Task<Diet?> GetWithEntriesAsync(int id) =>
        await _context.Diets
            .Include(d => d.FeedingEntries)
            .Include(d => d.Beehives)
                .ThenInclude(db => db.Beehive)
            .Include(d => d.Apiary)
            .Include(d => d.CreatedBy)
            .FirstOrDefaultAsync(d => d.Id == id);

    public async Task<Dictionary<int, List<DietActiveInfo>>> GetActiveForBeehivesAsync(IReadOnlyCollection<int> beehiveIds)
    {
        if (beehiveIds.Count == 0) return [];

        // Filtered in SQL, exactly as TreatmentRepository.GetLatestForBeehivesAsync does — only the
        // "next round" fold below runs in memory, over a handful of rows per programme.
        var rows = await _context.DietBeehives
            .AsNoTracking()
            .Where(db => db.RemovedOn == null && beehiveIds.Contains(db.BeehiveId))
            .Where(db => db.Diet.Status == DietStatus.NotStarted || db.Diet.Status == DietStatus.InProgress)
            .Select(db => new
            {
                db.BeehiveId,
                DietId   = db.Diet.Id,
                DietName = db.Diet.Name,
                db.Diet.FoodType,
                db.Diet.CustomFoodType,
                db.Diet.AmountPerHive,
                db.Diet.AmountUnit,
                db.Diet.AmountNote,
                db.Diet.StartDate,
                Rounds = db.Diet.FeedingEntries
                    .Select(e => new { e.Status, e.ScheduledDate })
                    .ToList(),
            })
            .ToListAsync();

        var today = DateTime.UtcNow.Date;

        return rows
            .GroupBy(r => r.BeehiveId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r =>
                    {
                        var pending = r.Rounds
                            .Where(x => x.Status == FeedingEntryStatus.Pending)
                            .Select(x => x.ScheduledDate)
                            .OrderBy(d => d)
                            .ToList();

                        return new DietActiveInfo(
                            r.BeehiveId, r.DietId, r.DietName, r.FoodType, r.CustomFoodType,
                            r.AmountPerHive, r.AmountUnit, r.AmountNote,
                            r.StartDate, NextRound(pending, today),
                            r.Rounds.Count(x => x.Status == FeedingEntryStatus.Completed),
                            r.Rounds.Count);
                    })
                    .OrderBy(x => x.StartDate)
                    .ToList());
    }

    /// <summary>Earliest round still ahead of us; if the programme is behind, the oldest overdue one.</summary>
    private static DateTime? NextRound(List<DateTime> pendingAscending, DateTime today)
    {
        foreach (var d in pendingAscending)
            if (d.Date >= today) return d;

        return pendingAscending.Count > 0 ? pendingAscending[0] : null;
    }
}
