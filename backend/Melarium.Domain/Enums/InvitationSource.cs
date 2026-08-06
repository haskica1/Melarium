namespace Melarium.Domain.Enums;

/// <summary>
/// How an invitation row came to exist (SPEC-15). The two paths are genuinely different events,
/// not two states of one: an <see cref="EmailInvite"/> is born when the inviter types an address,
/// a <see cref="ShareLink"/> row is born only at the moment someone registers through the link —
/// there was never a "send" to record.
/// </summary>
public enum InvitationSource
{
    /// <summary>The inviter typed an address and we mailed it. The only source the daily send cap counts.</summary>
    EmailInvite = 1,

    /// <summary>Someone registered through the inviter's personal link. Created already <c>Accepted</c>.</summary>
    ShareLink = 2,
}
