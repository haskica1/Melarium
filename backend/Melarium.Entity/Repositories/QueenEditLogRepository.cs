using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class QueenEditLogRepository : Repository<QueenEditLog>, IQueenEditLogRepository
{
    public QueenEditLogRepository(MelariumDbContext context) : base(context) { }

    public async Task<IEnumerable<QueenEditLog>> GetByQueenIdAsync(int queenId) =>
        await _context.QueenEditLogs
            .AsNoTracking()
            .Include(l => l.EditedBy)
            .Where(l => l.QueenId == queenId)
            .OrderByDescending(l => l.CreatedAt)
            .ThenByDescending(l => l.Id)
            .ToListAsync();
}
