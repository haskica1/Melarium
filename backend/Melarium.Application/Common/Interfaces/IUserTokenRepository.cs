using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Single-use emailed tokens — password reset and email verification.</summary>
public interface IUserTokenRepository : IRepository<UserToken>
{
    /// <summary>Returns the (tracked) token matching both hash and purpose, or null.</summary>
    Task<UserToken?> GetByHashAsync(string tokenHash, UserTokenPurpose purpose);

    /// <summary>Returns the user's unused, unexpired tokens of the given purpose (tracked).</summary>
    Task<IEnumerable<UserToken>> GetActiveByUserAsync(int userId, UserTokenPurpose purpose);
}
