using Melarium.Domain.Enums;

namespace Melarium.Application.Common.Security;

/// <summary>
/// Grants plan time to an organization (SPEC-15). Deliberately separate from
/// <see cref="IPlanGuard"/>: the guard <b>refuses</b> actions and depends on who is asking, while
/// this <b>gives</b> and must behave the same no matter who — or what — triggered it.
/// </summary>
/// <remarks>
/// This is the only place in the application allowed to do arithmetic on
/// <c>Organization.PlanValidUntil</c>. The other two writers assign an absolute value chosen by a
/// human (<c>AdminService</c>, SystemAdmin) or by registration (<c>AuthService</c>, the trial).
/// Anything that grants days — referral rewards today, a promotion or a goodwill credit tomorrow —
/// goes through here so the invariants in <see cref="PlanCredit"/> are stated once.
/// <para>
/// It never calls <c>SaveChangesAsync</c>: the caller owns the transaction boundary, because on the
/// referral path the grant must not be able to fail the invitee's own request.
/// </para>
/// </remarks>
public interface IPlanCredit
{
    /// <summary>
    /// Adds <paramref name="days"/> to the organization's plan, honouring every invariant in
    /// <see cref="PlanCredit.GrantDays"/>. Mutates the tracked entity; does not save.
    /// </summary>
    Task<PlanCreditResult> GrantDaysAsync(int organizationId, int days);
}

/// <summary>
/// What a grant actually did — the caller words its message from this and never assumes.
/// A fixed "you received N days of Pro" string would be wrong for a Standard customer (whose plan
/// is untouched) and absurd for a lifetime plan (which receives nothing).
/// </summary>
/// <param name="DaysGranted">0 when the organization is on a lifetime plan and there is nothing to extend.</param>
/// <param name="ResultingPlan">The plan after the grant — unchanged except on an upgrade.</param>
/// <param name="WasUpgrade">True only when an expired/Free organization was raised to Pro.</param>
public readonly record struct PlanCreditResult(int DaysGranted, PlanType ResultingPlan, bool WasUpgrade);
