using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class AdvisorConversationRepository : Repository<AdvisorConversation>, IAdvisorConversationRepository
{
    public AdvisorConversationRepository(MelariumDbContext context) : base(context) { }

    public async Task<IEnumerable<AdvisorConversation>> GetByUserAsync(int userId) =>
        await _context.AdvisorConversations
            .AsNoTracking()
            .Include(c => c.Beehive)
            .Where(c => c.UserId == userId)
            // UpdatedAt is bumped on every exchange, so it doubles as "last activity".
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .ToListAsync();

    public async Task<AdvisorConversation?> GetWithMessagesAsync(int id) =>
        await _context.AdvisorConversations
            .Include(c => c.Beehive)
            .Include(c => c.Messages.OrderBy(m => m.Id))
            .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<int> CountUserMessagesForOrganizationSinceAsync(int organizationId, DateTime sinceUtc) =>
        await _context.AdvisorMessages.CountAsync(m =>
            m.Role == Melarium.Domain.Enums.AdvisorRole.User &&
            m.CreatedAt >= sinceUtc &&
            m.Conversation.User.OrganizationId == organizationId);
}
