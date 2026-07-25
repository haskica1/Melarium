using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class UserTokenRepository : Repository<UserToken>, IUserTokenRepository
{
    public UserTokenRepository(MelariumDbContext context) : base(context) { }

    // Tracked (no AsNoTracking) so the caller can stamp UsedAt on the returned token.
    public async Task<UserToken?> GetByHashAsync(string tokenHash, UserTokenPurpose purpose) =>
        await _context.UserTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.Purpose == purpose);

    public async Task<IEnumerable<UserToken>> GetActiveByUserAsync(int userId, UserTokenPurpose purpose) =>
        await _context.UserTokens
            .Where(t => t.UserId == userId
                        && t.Purpose == purpose
                        && t.UsedAt == null
                        && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();
}
