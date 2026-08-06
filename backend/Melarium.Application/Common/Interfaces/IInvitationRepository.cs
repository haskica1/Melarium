using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>
/// Invitations to the platform (SPEC-15). Every count that feeds a cap is a SQL aggregate, never a
/// list materialised and counted in memory — these run on the registration and verification paths.
/// </summary>
public interface IInvitationRepository : IRepository<Invitation>
{
    /// <summary>The caller's own invitations, newest first.</summary>
    Task<IEnumerable<Invitation>> GetByInviterAsync(int inviterUserId);

    /// <summary>
    /// The most recent still-<c>Sent</c> invitation to this canonical address, used as the
    /// attribution fallback when someone registers without following the link. Most recent rather
    /// than oldest: if three people invited the same person over three months, the latest one is
    /// the likeliest reason they finally signed up.
    /// </summary>
    Task<Invitation?> GetLatestPendingByCanonicalEmailAsync(string emailCanonical);

    /// <summary>
    /// The accepted-but-unrewarded invitation that produced this user, resolved at the moment they
    /// verify their e-mail. Null when they did not arrive through an invitation, or were already paid.
    /// </summary>
    Task<Invitation?> GetUnrewardedByAcceptedUserAsync(int acceptedByUserId);

    /// <summary>Lifetime reward days already granted to an organisation — the 180-day cap.</summary>
    Task<int> SumRewardDaysForOrganizationAsync(int inviterOrganizationId);

    /// <summary>Rewards granted to an organisation since a cut-off — the rolling 5-per-30-days cap.</summary>
    Task<int> CountRewardsForOrganizationSinceAsync(int inviterOrganizationId, DateTime since);

    /// <summary>
    /// Whether any invitation has already paid out for this newly created organisation. Closes
    /// "invite the same person from two of my own accounts".
    /// </summary>
    Task<bool> AnyRewardForAcceptedOrganizationAsync(int acceptedOrganizationId);

    /// <summary>Counts for the stats strip, in one round trip.</summary>
    Task<(int Sent, int Accepted, int RewardDays)> GetSummaryForInviterAsync(int inviterUserId);
}
