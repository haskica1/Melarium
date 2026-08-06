using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class InvitationRepository : Repository<Invitation>, IInvitationRepository
{
    public InvitationRepository(MelariumDbContext context) : base(context) { }

    public async Task<IEnumerable<Invitation>> GetByInviterAsync(int inviterUserId) =>
        await _context.Invitations
            .AsNoTracking()
            .Where(i => i.InviterUserId == inviterUserId)
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .ToListAsync();

    public async Task<Invitation?> GetLatestPendingByCanonicalEmailAsync(string emailCanonical) =>
        await _context.Invitations
            .Where(i => i.EmailCanonical == emailCanonical && i.Status == InvitationStatus.Sent)
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .FirstOrDefaultAsync();

    public async Task<Invitation?> GetUnrewardedByAcceptedUserAsync(int acceptedByUserId) =>
        await _context.Invitations
            .Where(i => i.AcceptedByUserId == acceptedByUserId && i.RewardGrantedAt == null)
            .OrderBy(i => i.CreatedAt)
            .FirstOrDefaultAsync();

    public async Task<int> SumRewardDaysForOrganizationAsync(int inviterOrganizationId) =>
        await _context.Invitations
            .Where(i => i.InviterOrganizationId == inviterOrganizationId && i.RewardGrantedAt != null)
            .SumAsync(i => i.RewardDays ?? 0);

    public async Task<int> CountRewardsForOrganizationSinceAsync(int inviterOrganizationId, DateTime since) =>
        await _context.Invitations
            .CountAsync(i => i.InviterOrganizationId == inviterOrganizationId
                          && i.RewardGrantedAt != null
                          && i.RewardGrantedAt >= since);

    public async Task<bool> AnyRewardForAcceptedOrganizationAsync(int acceptedOrganizationId) =>
        await _context.Invitations
            .AnyAsync(i => i.AcceptedOrganizationId == acceptedOrganizationId && i.RewardGrantedAt != null);

    public async Task<(int Sent, int Accepted, int RewardDays)> GetSummaryForInviterAsync(int inviterUserId)
    {
        var summary = await _context.Invitations
            .AsNoTracking()
            .Where(i => i.InviterUserId == inviterUserId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                // Only e-mailed rows were ever "sent" — a share-link row is born already accepted,
                // so counting it as sent would overstate what the user actually did.
                Sent = g.Count(i => i.Source == InvitationSource.EmailInvite),
                Accepted = g.Count(i => i.Status == InvitationStatus.Accepted),
                RewardDays = g.Sum(i => i.RewardDays ?? 0),
            })
            .FirstOrDefaultAsync();

        return summary is null ? (0, 0, 0) : (summary.Sent, summary.Accepted, summary.RewardDays);
    }
}
