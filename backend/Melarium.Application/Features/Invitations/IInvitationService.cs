using Melarium.Application.Features.Invitations.DTOs;

namespace Melarium.Application.Features.Invitations;

/// <summary>
/// "Pozovi prijatelja" (SPEC-15) — Phase 1: the personal share link, attribution, and the reward.
/// </summary>
/// <remarks>
/// The three <c>Try…</c> members are seams called from <c>AuthService</c>. Every one of them is
/// written so that <b>nothing it does can fail the caller's request</b>: a referral is a nice-to-have
/// bolted onto sign-up and verification, and losing attribution is always cheaper than losing the
/// account. They swallow their own exceptions and log.
/// </remarks>
public interface IInvitationService
{
    /// <summary>Stats + share URL for the caller, minting their referral code on first use.</summary>
    Task<InvitationSummaryDto> GetSummaryAsync();

    /// <summary>The caller's own invitations, newest first.</summary>
    Task<IEnumerable<InvitationDto>> GetMineAsync();

    /// <summary>Resolves a referral code to the inviter's first name for the /register banner; null when unknown.</summary>
    Task<ReferralInviterDto?> GetInviterByCodeAsync(string code);

    /// <summary>
    /// How many trial days a registration should receive. Returns the invited length when the code
    /// or the address matches a real invitation, otherwise the standard trial.
    /// <b>Never throws</b> — it runs before the organization exists, so a failure here would fail
    /// the sign-up itself.
    /// </summary>
    Task<int> ResolveTrialDaysAsync(string? referralCode, string email);

    /// <summary>
    /// Credits the inviter for a completed registration: flips a matching invitation to
    /// <c>Accepted</c>, or creates a share-link row. Call <b>after the last save</b> in
    /// <c>RegisterAsync</c> — see the remarks on this interface.
    /// </summary>
    Task TryAttributeRegistrationAsync(int newUserId, int newOrganizationId, string email, string? referralCode);

    /// <summary>
    /// Settles the reward once the invitee proves the address is real. Call <b>after the
    /// verification has been committed</b>: this shares the request's <c>IUnitOfWork</c>, so a
    /// failure here would otherwise poison the very <c>SaveChanges</c> that persists
    /// <c>EmailVerifiedAt</c>.
    /// </summary>
    Task TryGrantRewardForVerifiedUserAsync(int userId);
}
