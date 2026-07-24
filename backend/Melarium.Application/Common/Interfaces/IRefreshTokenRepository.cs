using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>RefreshToken-specific data access operations.</summary>
public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    /// <summary>Returns the (tracked) refresh token with the given hash, or null.</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash);

    /// <summary>Returns all non-revoked, non-expired refresh tokens for a user (tracked).</summary>
    Task<IEnumerable<RefreshToken>> GetActiveByUserAsync(int userId);
}
