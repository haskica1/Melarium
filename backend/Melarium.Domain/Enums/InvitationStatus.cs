namespace Melarium.Domain.Enums;

/// <summary>
/// Lifecycle of an invitation (SPEC-15). There is deliberately no "Expired" value: expiry is
/// computed from <c>CreatedAt</c> the way <c>PlanHelper</c> computes the effective plan, so no job
/// has to sweep the table. There is also no "Suppressed" value — an address that already has an
/// account receives a different email rather than silence, so there is nothing to suppress.
/// </summary>
public enum InvitationStatus
{
    /// <summary>Mailed, or link handed out, and not yet redeemed.</summary>
    Sent = 1,

    /// <summary>The invitee registered. The reward is a separate moment — see <c>Invitation.RewardGrantedAt</c>.</summary>
    Accepted = 2,
}
