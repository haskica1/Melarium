using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

/// <summary>
/// A short-lived, single-use token emailed to a user — password reset or email verification.
/// Only the SHA-256 hash is stored (same model as <see cref="RefreshToken"/>): a leaked database
/// row cannot be replayed as a link. <see cref="Purpose"/> is part of the lookup, so a verification
/// token can never be redeemed as a password reset.
///
/// One table with a purpose discriminator rather than two near-identical ones — the lifecycle
/// (issue → email → redeem once → expire) is the same for both.
/// </summary>
public class UserToken : BaseEntity
{
    /// <summary>SHA-256 hash (hex) of the raw token value.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public UserTokenPurpose Purpose { get; set; }

    public DateTime ExpiresAt { get; set; }

    /// <summary>Set the moment the token is redeemed — a token is valid exactly once.</summary>
    public DateTime? UsedAt { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
