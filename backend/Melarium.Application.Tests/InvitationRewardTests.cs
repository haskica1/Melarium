using Melarium.Application.Common.Security;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// The referral reward (SPEC-15) is the first thing in this app that writes plan value without a
/// human deciding it, so every branch of <see cref="PlanCredit.GrantDays"/> is locked here rather
/// than left to a manual pass — the dangerous cases need a Partner or an expired organization in
/// front of you, which nobody stumbles into while clicking around.
/// </summary>
public class InvitationRewardTests
{
    private static readonly DateTime Now = new(2026, 08, 06, 10, 30, 0, DateTimeKind.Utc);
    private const int Days = 30;

    // ── The one that silently destroys an account ──────────────────────────────

    [Theory]
    [InlineData(PlanType.Partner)]
    [InlineData(PlanType.Max)]
    [InlineData(PlanType.Pro)]
    [InlineData(PlanType.Standard)]
    public void LifetimePlan_IsNeverGivenAnExpiryDate(PlanType plan)
    {
        var org = new Organization { Plan = plan, PlanValidUntil = null };

        var result = PlanCredit.GrantDays(org, Days, Now);

        Assert.Null(org.PlanValidUntil);        // still "bez isteka"
        Assert.Equal(plan, org.Plan);
        Assert.Equal(0, result.DaysGranted);
        Assert.False(result.WasUpgrade);
    }

    // ── Upgrade: expired or genuinely Free ─────────────────────────────────────

    [Fact]
    public void ExpiredPlan_IsUpgradedToPro_CountedFromToday()
    {
        var org = new Organization { Plan = PlanType.Pro, PlanValidUntil = Now.Date.AddDays(-40) };

        var result = PlanCredit.GrantDays(org, Days, Now);

        Assert.Equal(PlanType.Pro, org.Plan);
        Assert.Equal(Now.Date.AddDays(Days), org.PlanValidUntil);
        Assert.True(result.WasUpgrade);
        Assert.Equal(Days, result.DaysGranted);
    }

    [Fact]
    public void FreePlan_WithNoExpiry_IsUpgradedToPro()
    {
        var org = new Organization { Plan = PlanType.Free, PlanValidUntil = null };

        var result = PlanCredit.GrantDays(org, Days, Now);

        Assert.Equal(PlanType.Pro, org.Plan);
        Assert.Equal(Now.Date.AddDays(Days), org.PlanValidUntil);
        Assert.True(result.WasUpgrade);
    }

    // ── Extend: trial and paying customers ─────────────────────────────────────

    [Fact]
    public void ActiveTrial_IsExtended_AndPlanIsUntouched()
    {
        var trialEnd = Now.Date.AddDays(12);
        var org = new Organization { Plan = PlanType.Pro, PlanValidUntil = trialEnd, PlanNotes = "Probni period" };

        var result = PlanCredit.GrantDays(org, Days, Now);

        Assert.Equal(trialEnd.AddDays(Days), org.PlanValidUntil);
        Assert.Equal(PlanType.Pro, org.Plan);
        Assert.False(result.WasUpgrade);
    }

    [Theory]
    [InlineData(PlanType.Standard)]
    [InlineData(PlanType.Max)]
    [InlineData(PlanType.Partner)]
    public void PayingCustomer_GetsDaysButKeepsTheirPlan(PlanType plan)
    {
        var until = Now.Date.AddDays(200);
        var org = new Organization { Plan = plan, PlanValidUntil = until };

        var result = PlanCredit.GrantDays(org, Days, Now);

        Assert.Equal(plan, org.Plan);                  // never "upgraded" to Pro — that is a downgrade for Max/Partner
        Assert.Equal(until.AddDays(Days), org.PlanValidUntil);
        Assert.False(result.WasUpgrade);
        Assert.Equal(plan, result.ResultingPlan);
    }

    // ── The date never moves backwards ─────────────────────────────────────────

    [Fact]
    public void ExpiryNeverMovesBackwards_WhenThePlanLapsedYesterday()
    {
        var org = new Organization { Plan = PlanType.Standard, PlanValidUntil = Now.Date.AddDays(-1) };

        PlanCredit.GrantDays(org, Days, Now);

        // Not (yesterday + 30). An expired plan is Free in effect, so it is an upgrade from today.
        Assert.Equal(Now.Date.AddDays(Days), org.PlanValidUntil);
        Assert.True(org.PlanValidUntil >= Now.Date);
    }

    [Fact]
    public void RepeatedGrants_Accumulate_AndOnlyEverMoveForward()
    {
        var org = new Organization { Plan = PlanType.Pro, PlanValidUntil = Now.Date.AddDays(5) };

        PlanCredit.GrantDays(org, Days, Now);
        var afterFirst = org.PlanValidUntil;
        PlanCredit.GrantDays(org, Days, Now);

        Assert.Equal(afterFirst!.Value.AddDays(Days), org.PlanValidUntil);
        Assert.True(org.PlanValidUntil > afterFirst);
    }

    // ── PlanNotes is not ours to write ─────────────────────────────────────────

    [Theory]
    [InlineData("Probni period")]
    [InlineData("Uplatnica #123")]
    [InlineData(null)]
    public void PlanNotes_IsNeverTouched(string? notes)
    {
        // PlansPage detects the trial with `planNotes === 'Probni period'` — an exact match. Writing
        // an audit line here would silently remove the trial notice from the plans page.
        var org = new Organization { Plan = PlanType.Pro, PlanValidUntil = Now.Date.AddDays(10), PlanNotes = notes };

        PlanCredit.GrantDays(org, Days, Now);

        Assert.Equal(notes, org.PlanNotes);
    }

    // ── Degenerate input ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void NonPositiveDays_ChangeNothing(int days)
    {
        var until = Now.Date.AddDays(10);
        var org = new Organization { Plan = PlanType.Standard, PlanValidUntil = until };

        var result = PlanCredit.GrantDays(org, days, Now);

        Assert.Equal(until, org.PlanValidUntil);
        Assert.Equal(PlanType.Standard, org.Plan);
        Assert.Equal(0, result.DaysGranted);
    }
}
