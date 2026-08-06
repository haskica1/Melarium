namespace Melarium.Application.Features.Invitations.DTOs;

/// <summary>
/// What /register shows in the "you were invited by…" banner (SPEC-15).
/// <b>First name only</b> — never a surname, never an e-mail address. A referral code identifies an
/// invitation, not an account, so resolving one reveals nothing about any address; that is what makes
/// this endpoint safe to expose anonymously where a forgot-password lookup would not be.
/// </summary>
public record ReferralInviterDto(string InviterFirstName, int TrialDays);
