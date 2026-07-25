namespace Melarium.Application.Common.Security;

/// <summary>
/// Ends a user's signed-in sessions by revoking their refresh tokens. Anything that invalidates
/// the trust behind an existing session goes through here: a password change or reset, and any
/// change to a user's role, organisation or apiary.
/// </summary>
public interface ISessionRevoker
{
    /// <summary>
    /// Revokes every active refresh token for the user. Mutates tracked entities only — the caller
    /// owns the transaction and must call <c>SaveChangesAsync</c>.
    /// </summary>
    /// <returns>How many tokens were revoked.</returns>
    Task<int> RevokeAllAsync(int userId);
}
