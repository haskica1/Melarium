using Melarium.Domain.Common;
using Melarium.Domain.Enums;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// The effective plan is computed, not stored (SPEC-09): an expired paid plan behaves as Free,
/// and dates — not instants — decide the boundary (valid through the end of the expiry day).
/// </summary>
public class PlanHelperTests
{
    private static readonly DateTime Now = new(2026, 07, 06, 12, 00, 00, DateTimeKind.Utc);

    [Fact]
    public void NullValidUntil_KeepsPlan_Forever()
    {
        Assert.Equal(PlanType.Pro, PlanHelper.Effective(PlanType.Pro, null, Now));
        Assert.Equal(PlanType.Partner, PlanHelper.Effective(PlanType.Partner, null, Now));
    }

    [Fact]
    public void ExpiredYesterday_FallsBackToFree()
    {
        var yesterday = Now.AddDays(-1);
        Assert.Equal(PlanType.Free, PlanHelper.Effective(PlanType.Pro, yesterday, Now));
    }

    [Fact]
    public void ExpiringToday_StillValid_ThroughEndOfDay()
    {
        // Same calendar day, earlier instant than Now — must NOT be treated as expired.
        var earlierToday = new DateTime(2026, 07, 06, 00, 00, 00, DateTimeKind.Utc);
        Assert.Equal(PlanType.Standard, PlanHelper.Effective(PlanType.Standard, earlierToday, Now));
    }

    [Fact]
    public void ValidInFuture_KeepsPlan()
    {
        Assert.Equal(PlanType.Max, PlanHelper.Effective(PlanType.Max, Now.AddDays(30), Now));
    }

    [Fact]
    public void Free_StaysFree_Regardless()
    {
        Assert.Equal(PlanType.Free, PlanHelper.Effective(PlanType.Free, null, Now));
        Assert.Equal(PlanType.Free, PlanHelper.Effective(PlanType.Free, Now.AddDays(-100), Now));
    }
}
