using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

/// <summary>
/// One "pozovi prijatelja" invitation to the platform (SPEC-15) — someone who is not yet a customer.
/// The invitee gets their own organization; this is not a way to add a member to the inviter's.
/// </summary>
/// <remarks>
/// Not tenant-scoped, so it does not go through <c>IAccessGuard</c>: scoping is "your own rows" via
/// <see cref="InviterUserId"/>. Every FK is nullable with <c>ON DELETE SET NULL</c> — deleting an
/// account must not delete the ledger that justifies an organization's extended plan.
/// <para>
/// Rows exist because the rules need them: without them there is no per-recipient cooldown, no cap
/// on the days we give away, no "one reward per invited organization", and no list for the inviter.
/// </para>
/// </remarks>
public class Invitation : BaseEntity
{
    public InvitationSource Source { get; set; }

    public InvitationStatus Status { get; set; } = InvitationStatus.Sent;

    public int? InviterUserId { get; set; }
    public User? Inviter { get; set; }

    /// <summary>
    /// The inviter's organization at the time of invitation — the one that receives any reward.
    /// Snapshotted rather than resolved through <see cref="Inviter"/> later, because a user can
    /// change organizations and the days were earned by the org they were in.
    /// </summary>
    public int? InviterOrganizationId { get; set; }
    public Organization? InviterOrganization { get; set; }

    /// <summary>The address we actually mail: trimmed and lower-cased, otherwise untouched.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// <see cref="Email"/> with any "+tag" stripped — a comparison key only, never a destination.
    /// Dots are deliberately NOT stripped: they are significant at most providers, so removing them
    /// would collide genuinely different people.
    /// </summary>
    public string EmailCanonical { get; set; } = string.Empty;

    public string? PersonalMessage { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public int? AcceptedByUserId { get; set; }
    public User? AcceptedByUser { get; set; }

    /// <summary>
    /// The organization created by the invitee. The guard for "one reward per invited organization",
    /// which closes inviting the same person from two of your own accounts.
    /// </summary>
    public int? AcceptedOrganizationId { get; set; }
    public Organization? AcceptedOrganization { get; set; }

    /// <summary>
    /// Days actually granted to the inviter's organization — 0 when the org is on a lifetime plan
    /// and there is nothing to extend. Null until the reward moment.
    /// </summary>
    public int? RewardDays { get; set; }

    /// <summary>
    /// When the reward was settled. Set even when <see cref="RewardDays"/> is 0, so a lifetime-plan
    /// organization counts as paid-out and cannot be rewarded twice for the same invitee.
    /// </summary>
    public DateTime? RewardGrantedAt { get; set; }
}
