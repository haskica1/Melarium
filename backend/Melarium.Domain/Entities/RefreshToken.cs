using Melarium.Domain.Common;

namespace Melarium.Domain.Entities;

/// <summary>
/// A rotating refresh token. Only the SHA-256 hash of the token value is stored — the raw token is
/// never persisted. Each successful refresh rotates the token: the old one is revoked and linked to
/// its replacement. Presenting an already rotated/revoked token signals theft and triggers
/// revocation of the user's whole active token set.
/// </summary>
public class RefreshToken : BaseEntity
{
    /// <summary>SHA-256 hash (hex) of the raw token value.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    /// <summary>Hash of the token that replaced this one on rotation (for reuse detection).</summary>
    public string? ReplacedByTokenHash { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
