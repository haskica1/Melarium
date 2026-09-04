using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Localization;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Common.Security;

/// <inheritdoc cref="IPlanLock" />
public sealed class PlanLock : IPlanLock
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IConfiguration _config;

    // Scoped service, so these cache for one request. The access guard asks on every resource check
    // and a list endpoint asks once per row — recomputing would mean a pair of queries per hive.
    private readonly Dictionary<int, PlanLockResult> _byOrganization = [];
    private bool? _isReadOnly;

    public PlanLock(IUnitOfWork uow, ICurrentUser currentUser, IConfiguration config)
    {
        _uow = uow;
        _currentUser = currentUser;
        _config = config;
    }

    public async Task<PlanLockResult> GetForOrganizationAsync(int organizationId)
    {
        if (Bypass()) return PlanLockResult.Empty;
        if (_byOrganization.TryGetValue(organizationId, out var cached)) return cached;

        var effective = await GetEffectivePlanAsync(organizationId);
        var result = await ComputeAsync(organizationId, effective);

        _byOrganization[organizationId] = result;
        return result;
    }

    private async Task<PlanLockResult> ComputeAsync(int organizationId, PlanType effective)
    {
        var maxApiaries = Limit(effective, "MaxApiaries");
        var maxBeehives = Limit(effective, "MaxBeehives");

        // Max and Partner cap neither, so there is nothing to rank and no query to run.
        if (maxApiaries is null && maxBeehives is null) return PlanLockResult.Empty;

        var apiaries = (await _uow.Apiaries.GetAllByOrganizationAsync(organizationId))
            .Select(a => new PlanLockPolicy.ApiaryRow(a.Id, a.CreatedAt))
            .ToList();

        // The repository already drops merged-away hives (SPEC-19), which is exactly what the policy
        // needs: they do not count toward the limit, so they must not consume a slot either.
        var beehives = (await _uow.Beehives.GetByOrganizationAsync(organizationId))
            .Select(b => new PlanLockPolicy.BeehiveRow(b.Id, b.ApiaryId, b.CreatedAt))
            .ToList();

        return PlanLockPolicy.Locked(apiaries, beehives, maxApiaries, maxBeehives);
    }

    public Task<PlanLockResult> PreviewForPlanAsync(int organizationId, PlanType plan) =>
        ComputeAsync(organizationId, plan);

    public async Task<PlanLockResult> GetForCurrentUserAsync() =>
        _currentUser.OrganizationId is int organizationId
            ? await GetForOrganizationAsync(organizationId)
            : PlanLockResult.Empty;

    public async Task<bool> IsApiaryLockedAsync(int apiaryId)
    {
        if (Bypass()) return false;

        var apiary = await _uow.Apiaries.GetByIdAsync(apiaryId);
        if (apiary is null) return false;

        return (await GetForOrganizationAsync(apiary.OrganizationId)).ApiaryIds.Contains(apiaryId);
    }

    public async Task<bool> IsBeehiveLockedAsync(int beehiveId)
    {
        if (Bypass()) return false;

        var apiary = await ApiaryOfBeehiveAsync(beehiveId);
        if (apiary is null) return false;

        return (await GetForOrganizationAsync(apiary.OrganizationId)).BeehiveIds.Contains(beehiveId);
    }

    public async Task EnsureApiaryUnlockedAsync(int apiaryId)
    {
        if (Bypass()) return;

        var apiary = await _uow.Apiaries.GetByIdAsync(apiaryId);
        if (apiary is null) return;

        var locked = await GetForOrganizationAsync(apiary.OrganizationId);
        if (!locked.ApiaryIds.Contains(apiaryId)) return;

        var effective = await GetEffectivePlanAsync(apiary.OrganizationId);
        throw new PlanLimitException(
            $"Ovaj pčelinjak je zaključan — {BsLabels.Label(effective)} paket uključuje {ApiaryAllowance(effective)}. Nadogradite paket da mu ponovo pristupite.");
    }

    public async Task EnsureBeehiveUnlockedAsync(int beehiveId)
    {
        if (Bypass()) return;

        var apiary = await ApiaryOfBeehiveAsync(beehiveId);
        if (apiary is null) return;

        var locked = await GetForOrganizationAsync(apiary.OrganizationId);
        if (!locked.BeehiveIds.Contains(beehiveId)) return;

        var effective = await GetEffectivePlanAsync(apiary.OrganizationId);

        // A hive locks for two different reasons, and the message has to say which. Telling someone
        // their hive is over a 7-hive limit when they actually lost the whole apiary sends them
        // deleting hives that were never the problem.
        throw new PlanLimitException(locked.ApiaryIds.Contains(apiary.Id)
            ? $"Ova košnica je zaključana jer je zaključan i njen pčelinjak — {BsLabels.Label(effective)} paket uključuje {ApiaryAllowance(effective)}. Nadogradite paket da joj ponovo pristupite."
            : $"Ova košnica je zaključana — {BsLabels.Label(effective)} paket uključuje do {Limit(effective, "MaxBeehives")} košnica. Nadogradite paket da joj ponovo pristupite.");
    }

    public async Task<bool> IsCurrentUserReadOnlyAsync()
    {
        _isReadOnly ??= await ComputeReadOnlyAsync();
        return _isReadOnly.Value;
    }

    private async Task<bool> ComputeReadOnlyAsync()
    {
        if (Bypass()) return false;
        if (_currentUser.UserId is not int userId) return false;
        if (_currentUser.OrganizationId is not int organizationId) return false;

        var effective = await GetEffectivePlanAsync(organizationId);
        var limit = Limit(effective, "MaxMembers");
        if (limit is null) return false;   // Max/Partner do not cap members

        var members = (await _uow.Users.GetByOrganizationWithDetailsAsync(organizationId))
            .OrderBy(u => u.CreatedAt).ThenBy(u => u.Id)
            .ToList();

        var owner = OwnerOf(members);
        if (owner is not null && owner.Id == userId) return false;   // the owner is never read-only

        // Everyone else ranks oldest-first and the accounts past MaxMembers lose write access —
        // the same definition of "additional member" the create-side gate counts with
        // (total accounts minus the owner).
        var additional = members.Where(u => u.Id != owner?.Id).ToList();
        var rank = additional.FindIndex(u => u.Id == userId);

        return rank >= limit;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// The organization's owner: its oldest OrganizationAdmin, falling back to its oldest account
    /// when the organization has no admin at all (legacy rows) — so a one-person organization can
    /// never be locked out of its own data.
    /// </summary>
    private static User? OwnerOf(IReadOnlyList<User> membersOldestFirst) =>
        membersOldestFirst.FirstOrDefault(u => u.Role == UserRole.OrganizationAdmin)
        ?? membersOldestFirst.FirstOrDefault();

    private async Task<Apiary?> ApiaryOfBeehiveAsync(int beehiveId)
    {
        var beehive = await _uow.Beehives.GetByIdAsync(beehiveId);
        return beehive is null ? null : await _uow.Apiaries.GetByIdAsync(beehive.ApiaryId);
    }

    private string ApiaryAllowance(PlanType effective) =>
        Limit(effective, "MaxApiaries") is 1 ? "1 pčelinjak" : $"do {Limit(effective, "MaxApiaries")} pčelinjaka";

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
}
