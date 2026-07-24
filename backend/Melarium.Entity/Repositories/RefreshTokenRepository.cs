using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class RefreshTokenRepository : Repository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(MelariumDbContext context) : base(context) { }

    // Tracked (no AsNoTracking) so the caller can revoke/rotate the returned token.
    public async Task<RefreshToken?> GetByHashAsync(string tokenHash) =>
        await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

    public async Task<IEnumerable<RefreshToken>> GetActiveByUserAsync(int userId) =>
        await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();
}
