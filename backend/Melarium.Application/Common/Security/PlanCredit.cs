using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Common.Security;

public class PlanCredit : IPlanCredit
{
    private readonly IUnitOfWork _uow;

    public PlanCredit(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<PlanCreditResult> GrantDaysAsync(int organizationId, int days)
    {
        var org = await _uow.Organizations.GetByIdAsync(organizationId)
            ?? throw new NotFoundException(nameof(Organization), organizationId);

        return GrantDays(org, days, DateTime.UtcNow);
    }

    /// <summary>
    /// The whole reward algorithm, pure and synchronous so it can be unit-tested without a database.
    /// Mutates <paramref name="org"/>; the caller saves.
    /// </summary>
    /// <remarks>
    /// Three invariants, each of which has a way of failing silently rather than loudly:
    /// <list type="bullet">
    /// <item><b>A lifetime plan is never given an expiry date.</b> <c>PlanValidUntil == null</c> means
    /// "bez isteka" (Partner, early adopters). Writing <c>today + days</c> there would convert an
    /// unlimited plan into one that expires in a month — the single most destructive thing this
    /// method could do, which is why the check comes first.</item>
    /// <item><b>The plan is only ever raised, never lowered.</b> The test is
    /// <c>effective == Free</c>, not an ordinal comparison: <c>PlanType</c> runs
    /// Free=1, Standard=2, Pro=3, Max=4, Partner=5, so <c>Plan = Pro</c> on a Partner organization
    /// would be a downgrade from 5 to 3.</item>
    /// <item><b>The expiry date never moves backwards.</b> <c>Max(existing, today)</c> means an
    /// organization whose plan lapsed two days ago does not receive days counted from two days ago.</item>
    /// </list>
    /// <para>
    /// <c>PlanNotes</c> is deliberately never touched. <c>PlansPage</c> detects the registration
    /// trial with an exact string match on "Probni period", so writing an audit line there would
    /// silently erase the trial notice for exactly the users most likely to be inviting people. The
    /// itemised record lives on the <c>Invitation</c> rows instead.
    /// </para>
    /// </remarks>
    public static PlanCreditResult GrantDays(Organization org, int days, DateTime utcNow)
    {
        if (days <= 0)
            return new PlanCreditResult(0, org.Plan, WasUpgrade: false);

        // Lifetime plan — nothing to extend, and nothing may be written.
        if (org.PlanValidUntil is null && org.Plan != PlanType.Free)
            return new PlanCreditResult(0, org.Plan, WasUpgrade: false);

        var effective = PlanHelper.Effective(org.Plan, org.PlanValidUntil, utcNow);

        if (effective == PlanType.Free)
        {
            org.Plan = PlanType.Pro;
            org.PlanValidUntil = utcNow.Date.AddDays(days);
            return new PlanCreditResult(days, org.Plan, WasUpgrade: true);
        }

        var from = org.PlanValidUntil!.Value.Date > utcNow.Date ? org.PlanValidUntil.Value.Date : utcNow.Date;
        org.PlanValidUntil = from.AddDays(days);
        return new PlanCreditResult(days, org.Plan, WasUpgrade: false);
    }
}
