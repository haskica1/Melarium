using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Localization;
using Melarium.Application.Features.Plans.DTOs;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Common.Security;

public class PlanGuard : IPlanGuard
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IConfiguration _config;

    public PlanGuard(IUnitOfWork uow, ICurrentUser currentUser, IConfiguration config)
    {
        _uow = uow;
        _currentUser = currentUser;
        _config = config;
    }

    public async Task EnsureCanAddApiaryAsync(int organizationId)
    {
        if (Bypass()) return;
        var effective = await GetEffectivePlanAsync(organizationId);

        var limit = Limit(effective, "MaxApiaries");
        if (limit is null) return;

        var count = await _uow.Apiaries.CountByOrganizationAsync(organizationId);
        if (count >= limit)
            throw new PlanLimitException(
                limit == 1
                    ? $"{Label(effective)} paket uključuje 1 pčelinjak — nadogradite na {NextLabel(effective)} za više lokacija."
                    : $"{Label(effective)} paket uključuje do {limit} pčelinjaka — nadogradite na {NextLabel(effective)}.");
    }

    public async Task EnsureCanAddBeehiveAsync(int organizationId)
    {
        if (Bypass()) return;
        var effective = await GetEffectivePlanAsync(organizationId);

        var limit = Limit(effective, "MaxBeehives");
        if (limit is null) return;

        var count = await _uow.Beehives.CountByOrganizationAsync(organizationId);
        if (count >= limit)
            throw new PlanLimitException(
                $"{Label(effective)} paket uključuje do {limit} košnica — nadogradite na {NextLabel(effective)}.");
    }

    public async Task EnsureCanAddMemberAsync(int organizationId)
    {
        if (Bypass()) return;
        var effective = await GetEffectivePlanAsync(organizationId);

        var limit = Limit(effective, "MaxMembers");
        if (limit is null) return;

        var additional = await CountAdditionalMembersAsync(organizationId);
        if (additional >= limit)
            throw new PlanLimitException(limit == 0
                ? "Dodatni članovi su dio plaćenih paketa — nadogradite na Standard."
                : $"{Label(effective)} paket uključuje do {limit} {(limit is >= 2 and <= 4 ? "dodatna člana" : "dodatnih članova")} — nadogradite na {NextLabel(effective)}.");
    }

    public async Task EnsureFeatureAsync(int organizationId, PlanFeature feature)
    {
        if (Bypass()) return;
        var effective = await GetEffectivePlanAsync(organizationId);

        if (HasFeature(effective, feature)) return;

        throw new PlanLimitException(feature switch
        {
            PlanFeature.VoiceInput    => "Glasovni unos pregleda je dio plaćenih paketa — nadogradite na Standard.",
            PlanFeature.WeeklySummary => "Sedmični AI sažetak je dio plaćenih paketa — nadogradite na Standard.",
            PlanFeature.Pastures      => "Pašnjaci i selidbe su dio plaćenih paketa — nadogradite na Standard.",
            PlanFeature.PhotoAnalysis => "AI analiza fotografija je dio Pro paketa — nadogradite paket.",
            _                         => "Ova funkcija nije dostupna u vašem paketu.",
        });
    }

    public async Task EnsureAdvisorMessageAsync(int organizationId)
    {
        if (Bypass()) return;
        var effective = await GetEffectivePlanAsync(organizationId);

        if (effective == PlanType.Free)
            throw new PlanLimitException("AI savjetnik je dio plaćenih paketa — nadogradite na Standard.");

        var quota = Limit(effective, "AdvisorMessagesPerMonth");
        if (quota is null) return;

        var used = await CountAdvisorMessagesThisMonthAsync(organizationId);
        if (used >= quota)
            throw new PlanLimitException(
                $"Iskoristili ste {quota} AI poruka ovog mjeseca — Pro paket nema ograničenja.");
    }

    public async Task<MyPlanDto> GetMyPlanAsync(int organizationId)
    {
        var org = await _uow.Organizations.GetByIdAsync(organizationId)
            ?? throw new NotFoundException(nameof(Organization), organizationId);

        var effective = PlanHelper.Effective(org.Plan, org.PlanValidUntil, DateTime.UtcNow);

        var advisorQuota = effective == PlanType.Free ? 0 : Limit(effective, "AdvisorMessagesPerMonth");

        return new MyPlanDto
        {
            Plan = org.Plan,
            PlanName = BsLabels.Label(org.Plan),
            EffectivePlan = effective,
            EffectivePlanName = BsLabels.Label(effective),
            PlanValidUntil = org.PlanValidUntil,
            PlanNotes = org.PlanNotes,
            Usage = new PlanUsageDto
            {
                Apiaries = await _uow.Apiaries.CountByOrganizationAsync(organizationId),
                ApiariesLimit = Limit(effective, "MaxApiaries"),
                Beehives = await _uow.Beehives.CountByOrganizationAsync(organizationId),
                BeehivesLimit = Limit(effective, "MaxBeehives"),
                Members = await CountAdditionalMembersAsync(organizationId),
                MembersLimit = Limit(effective, "MaxMembers"),
                AdvisorMessagesThisMonth = await CountAdvisorMessagesThisMonthAsync(organizationId),
                AdvisorMessagesLimit = advisorQuota,
            },
        };
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>The org-less SystemAdmin is unaffected by plan gates (SPEC-09).</summary>
    private bool Bypass() => _currentUser.Role == UserRole.SystemAdmin;

    private async Task<PlanType> GetEffectivePlanAsync(int organizationId)
    {
        var org = await _uow.Organizations.GetByIdAsync(organizationId)
            ?? throw new NotFoundException(nameof(Organization), organizationId);
        return PlanHelper.Effective(org.Plan, org.PlanValidUntil, DateTime.UtcNow);
    }

    /// <summary>Config lookup <c>Plans:{plan}:{key}</c>; absent key = unlimited (null).</summary>
    private int? Limit(PlanType plan, string key) =>
        int.TryParse(_config[$"Plans:{plan}:{key}"], out var value) ? value : null;

    private static bool HasFeature(PlanType effective, PlanFeature feature) => feature switch
    {
        PlanFeature.PhotoAnalysis => effective >= PlanType.Pro,
        _                         => effective >= PlanType.Standard,
    };

    /// <summary>MaxMembers counts accounts beyond the first OrganizationAdmin (vlasnik).</summary>
    private async Task<int> CountAdditionalMembersAsync(int organizationId)
    {
        var total = await _uow.Users.CountByOrganizationAsync(organizationId);
        return Math.Max(0, total - 1);
    }

    /// <summary>Per-organization count, current UTC calendar month — resets implicitly on the 1st.</summary>
    private async Task<int> CountAdvisorMessagesThisMonthAsync(int organizationId)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return await _uow.AdvisorConversations.CountUserMessagesForOrganizationSinceAsync(organizationId, monthStart);
    }

    private static string Label(PlanType plan) => BsLabels.Label(plan);

    /// <summary>Upgrade suggestion for 402 messages: Free→Standard, Standard→Pro, ostalo→Max.</summary>
    private static string NextLabel(PlanType plan) => plan switch
    {
        PlanType.Free     => BsLabels.Label(PlanType.Standard),
        PlanType.Standard => BsLabels.Label(PlanType.Pro),
        _                 => BsLabels.Label(PlanType.Max),
    };
}
