using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class QueenRepository : Repository<Queen>, IQueenRepository
{
    public QueenRepository(MelariumDbContext context) : base(context) { }

    public async Task<IEnumerable<Queen>> GetByBeehiveIdAsync(int beehiveId) =>
        await _context.Queens
            .AsNoTracking()
            .Where(q => q.BeehiveId == beehiveId)
            .OrderByDescending(q => q.IntroducedDate)
            .ThenByDescending(q => q.Id)
            .ToListAsync();

    // Tracked on purpose — the service mutates the returned queen when replacing it.
    public async Task<Queen?> GetActiveByBeehiveIdAsync(int beehiveId) =>
        await _context.Queens
            .FirstOrDefaultAsync(q => q.BeehiveId == beehiveId && q.Status == QueenStatus.Active);

    public async Task<Dictionary<int, Queen>> GetActiveByBeehiveIdsAsync(IReadOnlyCollection<int> beehiveIds) =>
        await _context.Queens
            .AsNoTracking()
            .Where(q => q.Status == QueenStatus.Active && beehiveIds.Contains(q.BeehiveId))
            .ToDictionaryAsync(q => q.BeehiveId);
}
