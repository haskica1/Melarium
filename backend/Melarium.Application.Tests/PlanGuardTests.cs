using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Plan enforcement (SPEC-09): config-driven limits, feature gates per tier, the per-organization
/// monthly AI-interaction quota (SPEC-18, merged from the former separate advisor/assistant quotas),
/// and the org-less SystemAdmin bypass. Limits mirror the spec's defaults.
/// </summary>
public class PlanGuardTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    // The spec's default config (Free/Standard/Pro have entries; Max/Partner are absent = unlimited).
    private static readonly Dictionary<string, string?> DefaultPlans = new()
    {
        ["Plans:Free:MaxApiaries"] = "1",
        ["Plans:Free:MaxBeehives"] = "7",
        ["Plans:Free:MaxMembers"] = "0",
        ["Plans:Standard:MaxBeehives"] = "30",
        ["Plans:Standard:MaxMembers"] = "2",
        ["Plans:Standard:AiInteractionsPerMonth"] = "30",
        ["Plans:Pro:MaxBeehives"] = "100",
        ["Plans:Pro:MaxMembers"] = "5",
    };

    private static IConfiguration Config()
    {
        var config = Substitute.For<IConfiguration>();
        config[Arg.Any<string>()].Returns(ci => DefaultPlans.GetValueOrDefault(ci.Arg<string>()));
        return config;
    }

    private PlanGuard Guard(UserRole role = UserRole.OrganizationAdmin, int? orgId = 1) =>
        new(_uow, new TestCurrentUser { UserId = 1, Role = role, OrganizationId = orgId }, Config());

    private void OrgOnPlan(int orgId, PlanType plan, DateTime? validUntil = null) =>
        _uow.Organizations.GetByIdAsync(orgId)
            .Returns(new Organization { Id = orgId, Name = "Org", Plan = plan, PlanValidUntil = validUntil });

    // ── Apiary limit ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Free_SecondApiary_ThrowsPlanLimit()
    {
        OrgOnPlan(1, PlanType.Free);
        _uow.Apiaries.CountByOrganizationAsync(1).Returns(1); // already at the limit of 1

        var ex = await Assert.ThrowsAsync<PlanLimitException>(() => Guard().EnsureCanAddApiaryAsync(1));
        Assert.Contains("pčelinjak", ex.Message);
    }

    [Fact]
    public async Task Standard_ManyApiaries_Allowed_NoApiaryLimitConfigured()
    {
        OrgOnPlan(1, PlanType.Standard);
        _uow.Apiaries.CountByOrganizationAsync(1).Returns(50);

        await Guard().EnsureCanAddApiaryAsync(1); // no throw — Standard has no MaxApiaries key
    }

    // ── Beehive limit ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(PlanType.Free, 7)]
    [InlineData(PlanType.Standard, 30)]
    [InlineData(PlanType.Pro, 100)]
    public async Task AtBeehiveLimit_ThrowsPlanLimit(PlanType plan, int limit)
    {
        OrgOnPlan(1, plan);
        _uow.Beehives.CountByOrganizationAsync(1).Returns(limit);

        var ex = await Assert.ThrowsAsync<PlanLimitException>(() => Guard().EnsureCanAddBeehiveAsync(1));
        Assert.Contains("košnica", ex.Message);
    }

    [Fact]
    public async Task JustUnderBeehiveLimit_Allowed()
    {
        OrgOnPlan(1, PlanType.Free);
        _uow.Beehives.CountByOrganizationAsync(1).Returns(6); // 7th hive is allowed

        await Guard().EnsureCanAddBeehiveAsync(1);
    }

    [Theory]
    [InlineData(PlanType.Max)]
    [InlineData(PlanType.Partner)]
    public async Task Unlimited_Plans_NoBeehiveLimit(PlanType plan)
    {
        OrgOnPlan(1, plan);
        _uow.Beehives.CountByOrganizationAsync(1).Returns(10_000);

        await Guard().EnsureCanAddBeehiveAsync(1); // no throw
    }

    // ── Member limit (additional accounts beyond the owner) ───────────────────────

    [Fact]
    public async Task Free_FirstAdditionalMember_Throws()
    {
        OrgOnPlan(1, PlanType.Free);
        _uow.Users.CountByOrganizationAsync(1).Returns(1); // owner only → 0 additional, limit 0

        await Assert.ThrowsAsync<PlanLimitException>(() => Guard().EnsureCanAddMemberAsync(1));
    }

    [Fact]
    public async Task Standard_ThirdAdditionalMember_Throws()
    {
        OrgOnPlan(1, PlanType.Standard);
        _uow.Users.CountByOrganizationAsync(1).Returns(3); // owner + 2 additional = at the limit of 2

        await Assert.ThrowsAsync<PlanLimitException>(() => Guard().EnsureCanAddMemberAsync(1));
    }

    [Fact]
    public async Task Standard_SecondAdditionalMember_Allowed()
    {
        OrgOnPlan(1, PlanType.Standard);
        _uow.Users.CountByOrganizationAsync(1).Returns(2); // owner + 1 additional, adding the 2nd

        await Guard().EnsureCanAddMemberAsync(1);
    }

    // ── Feature gates ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(PlanFeature.VoiceInput)]
    [InlineData(PlanFeature.WeeklySummary)]
    [InlineData(PlanFeature.Pastures)]
    [InlineData(PlanFeature.PhotoAnalysis)]
    public async Task Free_AllGatedFeatures_Throw(PlanFeature feature)
    {
        OrgOnPlan(1, PlanType.Free);
        await Assert.ThrowsAsync<PlanLimitException>(() => Guard().EnsureFeatureAsync(1, feature));
    }

    [Theory]
    [InlineData(PlanFeature.VoiceInput)]
    [InlineData(PlanFeature.WeeklySummary)]
    [InlineData(PlanFeature.Pastures)]
    public async Task Standard_StandardFeatures_Allowed(PlanFeature feature)
    {
        OrgOnPlan(1, PlanType.Standard);
        await Guard().EnsureFeatureAsync(1, feature); // no throw
    }

    [Fact]
    public async Task Standard_PhotoAnalysis_Throws_ButPro_Allowed()
    {
        OrgOnPlan(1, PlanType.Standard);
        await Assert.ThrowsAsync<PlanLimitException>(() => Guard().EnsureFeatureAsync(1, PlanFeature.PhotoAnalysis));

        OrgOnPlan(2, PlanType.Pro);
        await Guard().EnsureFeatureAsync(2, PlanFeature.PhotoAnalysis); // no throw
    }

    // ── AI interaction monthly quota (per organization, SPEC-18) ──────────────────

    [Fact]
    public async Task Free_AiInteraction_Throws()
    {
        OrgOnPlan(1, PlanType.Free);
        await Assert.ThrowsAsync<PlanLimitException>(() => Guard().EnsureAiInteractionAsync(1));
    }

    [Fact]
    public async Task Standard_ThirtyFirstInteractionInMonth_Throws()
    {
        OrgOnPlan(1, PlanType.Standard);
        _uow.AiAssistantSessions
            .CountUserTurnsForOrganizationSinceAsync(1, Arg.Any<DateTime>())
            .Returns(30); // quota is 30 → the 31st is blocked

        var ex = await Assert.ThrowsAsync<PlanLimitException>(() => Guard().EnsureAiInteractionAsync(1));
        Assert.Contains("30 AI poruka", ex.Message);
    }

    [Fact]
    public async Task Standard_ThirtiethInteraction_Allowed()
    {
        OrgOnPlan(1, PlanType.Standard);
        _uow.AiAssistantSessions
            .CountUserTurnsForOrganizationSinceAsync(1, Arg.Any<DateTime>())
            .Returns(29); // sending the 30th

        await Guard().EnsureAiInteractionAsync(1);
    }

    [Fact]
    public async Task Pro_AiInteraction_Unlimited()
    {
        OrgOnPlan(1, PlanType.Pro);
        _uow.AiAssistantSessions
            .CountUserTurnsForOrganizationSinceAsync(1, Arg.Any<DateTime>())
            .Returns(9_999);

        await Guard().EnsureAiInteractionAsync(1); // no quota key for Pro → unlimited
    }

    [Fact]
    public async Task QuotaCountsFromStartOfCurrentUtcMonth()
    {
        OrgOnPlan(1, PlanType.Standard);
        _uow.AiAssistantSessions.CountUserTurnsForOrganizationSinceAsync(1, Arg.Any<DateTime>()).Returns(0);

        await Guard().EnsureAiInteractionAsync(1);

        var now = DateTime.UtcNow;
        var expectedMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        await _uow.AiAssistantSessions.Received(1)
            .CountUserTurnsForOrganizationSinceAsync(1, expectedMonthStart);
    }

    // ── Expiry → Free behaviour ────────────────────────────────────────────────────

    [Fact]
    public async Task ExpiredPro_BehavesAsFree_ForBeehiveLimit()
    {
        OrgOnPlan(1, PlanType.Pro, validUntil: DateTime.UtcNow.AddDays(-1));
        _uow.Beehives.CountByOrganizationAsync(1).Returns(7); // Free limit

        await Assert.ThrowsAsync<PlanLimitException>(() => Guard().EnsureCanAddBeehiveAsync(1));
    }

    // ── SystemAdmin bypass ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SystemAdmin_BypassesAllGates()
    {
        // No org set up at all — a gate that read the plan would NRE; bypass must short-circuit first.
        var guard = Guard(role: UserRole.SystemAdmin, orgId: null);

        await guard.EnsureCanAddApiaryAsync(999);
        await guard.EnsureCanAddBeehiveAsync(999);
        await guard.EnsureCanAddMemberAsync(999);
        await guard.EnsureFeatureAsync(999, PlanFeature.PhotoAnalysis);
        await guard.EnsureAiInteractionAsync(999);

        await _uow.Organizations.DidNotReceive().GetByIdAsync(Arg.Any<int>());
    }

    // ── GetMyPlan ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyPlan_ReportsEffectivePlanAndUsage()
    {
        OrgOnPlan(1, PlanType.Standard);
        _uow.Apiaries.CountByOrganizationAsync(1).Returns(2);
        _uow.Beehives.CountByOrganizationAsync(1).Returns(12);
        _uow.Users.CountByOrganizationAsync(1).Returns(2);
        _uow.AiAssistantSessions.CountUserTurnsForOrganizationSinceAsync(1, Arg.Any<DateTime>()).Returns(4);

        var dto = await Guard().GetMyPlanAsync(1);

        Assert.Equal(PlanType.Standard, dto.EffectivePlan);
        Assert.Equal("Standard", dto.PlanName);
        Assert.Null(dto.Usage.ApiariesLimit);      // Standard has no apiary cap
        Assert.Equal(30, dto.Usage.BeehivesLimit);
        Assert.Equal(1, dto.Usage.Members);         // 2 accounts − owner
        Assert.Equal(2, dto.Usage.MembersLimit);
        Assert.Equal(4, dto.Usage.AiInteractionsThisMonth);
        Assert.Equal(30, dto.Usage.AiInteractionsLimit);
    }

    [Fact]
    public async Task GetMyPlan_ExpiredPro_ReportsFreeEffectivePlan()
    {
        OrgOnPlan(1, PlanType.Pro, validUntil: DateTime.UtcNow.AddDays(-5));
        _uow.Apiaries.CountByOrganizationAsync(1).Returns(1);
        _uow.Beehives.CountByOrganizationAsync(1).Returns(3);
        _uow.Users.CountByOrganizationAsync(1).Returns(1);
        _uow.AiAssistantSessions.CountUserTurnsForOrganizationSinceAsync(1, Arg.Any<DateTime>()).Returns(0);

        var dto = await Guard().GetMyPlanAsync(1);

        Assert.Equal(PlanType.Pro, dto.Plan);           // stored plan unchanged
        Assert.Equal(PlanType.Free, dto.EffectivePlan); // but effectively Free
        Assert.Equal(1, dto.Usage.ApiariesLimit);
        Assert.Equal(7, dto.Usage.BeehivesLimit);
        Assert.Equal(0, dto.Usage.AiInteractionsLimit); // Free → no AI access
    }
}
