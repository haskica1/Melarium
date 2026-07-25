using Melarium.Application.Common.Interfaces;

namespace Melarium.Application.Common.Security;

/// <inheritdoc cref="ISessionRevoker" />
public sealed class SessionRevoker : ISessionRevoker
{
    private readonly IUnitOfWork _uow;

    public SessionRevoker(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<int> RevokeAllAsync(int userId)
    {
        var active = await _uow.RefreshTokens.GetActiveByUserAsync(userId);
        var now = DateTime.UtcNow;

        var count = 0;
        foreach (var token in active)
        {
            token.RevokedAt = now;
            count++;
        }

        return count;
    }
}
