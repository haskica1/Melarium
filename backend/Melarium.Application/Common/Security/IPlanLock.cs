using Melarium.Domain.Common;

namespace Melarium.Application.Common.Security;

/// <summary>
/// Downgrade locking (SPEC-24) — the counterpart to <see cref="IPlanGuard"/>. Where the guard
/// answers "may this organization create another one?", this answers "may it still reach the ones
/// it already has?" after falling to a smaller plan (a paid plan expiring, the registration trial
/// ending, or a SystemAdmin moving the organization down).
///
/// Everything here is <b>computed, never stored</b>, exactly like
/// <see cref="PlanHelper.Effective"/>: no migration, no background job, and an upgrade unlocks
/// everything the moment it lands. Which rows survive is decided by <see cref="PlanLockPolicy"/>
/// (oldest-first) — the organization does not choose.
///
/// Locked rows stay <b>visible</b> in list endpoints, flagged so the UI can grey them out; every
/// path that would open one or read its data throws
/// <see cref="Common.Exceptions.PlanLimitException"/> → HTTP 402, which the frontend already turns
/// into the upsell modal. Enforcement is wired into <see cref="IAccessGuard"/> rather than into
/// each service, so every feature that already checks access — inspections, harvests, queens,
/// treatments, todos, feeding, photos, the AI assistant — is covered without touching it.
///
/// The SystemAdmin bypasses all of it, as with every other plan gate.
/// </summary>
public interface IPlanLock
{
    /// <summary>
    /// The organization's locked apiary and beehive ids. Cached for the lifetime of the request,
    /// since the access guard consults it on every resource check.
    /// </summary>
    Task<PlanLockResult> GetForOrganizationAsync(int organizationId);

    /// <summary>Locked ids for the caller's own organization (empty for the org-less SystemAdmin).</summary>
    Task<PlanLockResult> GetForCurrentUserAsync();

    /// <summary>
    /// What <paramref name="plan"/> <i>would</i> lock, ignoring the organization's current plan and
    /// the caller entirely. Used by the two-day warning to say what is about to stop opening — and to
    /// stay silent for organizations that lose nothing.
    /// </summary>
    Task<PlanLockResult> PreviewForPlanAsync(int organizationId, Domain.Enums.PlanType plan);

    Task<bool> IsApiaryLockedAsync(int apiaryId);
    Task<bool> IsBeehiveLockedAsync(int beehiveId);

    /// <summary>Throws <see cref="Common.Exceptions.PlanLimitException"/> when the apiary is locked.</summary>
    Task EnsureApiaryUnlockedAsync(int apiaryId);

    /// <summary>Throws <see cref="Common.Exceptions.PlanLimitException"/> when the beehive is locked.</summary>
    Task EnsureBeehiveUnlockedAsync(int beehiveId);

    /// <summary>
    /// True when the caller is an additional member past the effective plan's <c>MaxMembers</c> and
    /// is therefore read-only. Ranked oldest-first among the organization's accounts; the owner (the
    /// organization's first OrganizationAdmin) is never counted and never read-only.
    /// </summary>
    Task<bool> IsCurrentUserReadOnlyAsync();
}
