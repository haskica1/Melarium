using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class AiAssistantSessionRepository : Repository<AiAssistantSession>, IAiAssistantSessionRepository
{
    public AiAssistantSessionRepository(MelariumDbContext context) : base(context) { }

    public async Task<IEnumerable<AiAssistantSession>> GetByUserAsync(int userId) =>
        await _context.AiAssistantSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            // UpdatedAt is bumped on every turn, so it doubles as "last activity".
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .ToListAsync();

    public async Task<AiAssistantSession?> GetWithTurnsAsync(int id) =>
        await _context.AiAssistantSessions
            .Include(s => s.Turns.OrderBy(t => t.Id))
                .ThenInclude(t => t.Actions.OrderBy(a => a.Id))
            .FirstOrDefaultAsync(s => s.Id == id);

    public async Task<AiAssistantTurn?> GetTurnWithActionsAsync(int turnId) =>
        await _context.AiAssistantTurns
            .Include(t => t.Session)
            .Include(t => t.Actions.OrderBy(a => a.Id))
            .FirstOrDefaultAsync(t => t.Id == turnId);

    public async Task<int> CountUserTurnsForOrganizationSinceAsync(int organizationId, DateTime sinceUtc) =>
        await _context.AiAssistantTurns.CountAsync(t =>
            t.Role == AiTurnRole.User &&
            t.CreatedAt >= sinceUtc &&
            t.Session.User.OrganizationId == organizationId);
}
