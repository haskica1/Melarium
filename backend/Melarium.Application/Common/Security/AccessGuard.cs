using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Common.Security;

/// <inheritdoc cref="IAccessGuard" />
public sealed class AccessGuard : IAccessGuard
{
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;
    private readonly IPlanLock _planLock;

    public AccessGuard(ICurrentUser user, IUnitOfWork uow, IPlanLock planLock)
    {
        _user = user;
        _uow = uow;
        _planLock = planLock;
    }

    public bool IsSystemAdmin => _user.Role == UserRole.SystemAdmin;

    public void EnsureInOrganization(int organizationId)
    {
        if (_user.Role == UserRole.SystemAdmin) return;
        if (_user.OrganizationId == organizationId) return;
        throw new ForbiddenAccessException();
    }

    public void EnsureCanManageApiary(int apiaryId, int organizationId)
    {
        switch (_user.Role)
        {
            case UserRole.SystemAdmin:
                return;
            case UserRole.OrganizationAdmin when _user.OrganizationId == organizationId:
                return;
            case UserRole.ApiaryAdmin when _user.ApiaryId == apiaryId:
                return;
            default:
                throw new ForbiddenAccessException();
        }
    }

    public async Task EnsureCanManageApiaryAsync(int apiaryId, bool allowLocked = false)
    {
        if (!await HasRoleAccessToApiaryAsync(apiaryId))
            throw new ForbiddenAccessException();

        // Role first, plan second: the two denials mean different things to the caller (403 "not
        // yours" vs 402 "yours, but above your plan"), and only the second one should offer an upsell.
        if (!allowLocked)
            await _planLock.EnsureApiaryUnlockedAsync(apiaryId);
    }

    public async Task<bool> CanManageApiaryAsync(int apiaryId) =>
        await HasRoleAccessToApiaryAsync(apiaryId)
        && !await _planLock.IsApiaryLockedAsync(apiaryId);

    public async Task EnsureCanAccessBeehiveAsync(int beehiveId, bool allowLocked = false)
    {
        if (!await HasRoleAccessToBeehiveAsync(beehiveId))
            throw new ForbiddenAccessException();

        if (!allowLocked)
            await _planLock.EnsureBeehiveUnlockedAsync(beehiveId);
    }

    public async Task<bool> CanAccessBeehiveAsync(int beehiveId) =>
        await HasRoleAccessToBeehiveAsync(beehiveId)
        && !await _planLock.IsBeehiveLockedAsync(beehiveId);

    // ── Role rules, without the plan lock ─────────────────────────────────────────

    private async Task<bool> HasRoleAccessToApiaryAsync(int apiaryId)
    {
        if (_user.Role == UserRole.SystemAdmin) return true;

        // An ApiaryAdmin is bound to a single apiary id, so no lookup is needed.
        if (_user.Role == UserRole.ApiaryAdmin) return _user.ApiaryId == apiaryId;

        var apiary = await _uow.Apiaries.GetByIdAsync(apiaryId);
        return apiary is not null
            && _user.Role == UserRole.OrganizationAdmin
            && _user.OrganizationId == apiary.OrganizationId;
    }

    private async Task<bool> HasRoleAccessToBeehiveAsync(int beehiveId)
    {
        switch (_user.Role)
        {
            case UserRole.SystemAdmin:
                return true;

            case UserRole.Beekeeper:
                return _user.UserId is int beekeeperId
                    && await _uow.Users.IsUserAssignedToBeehiveAsync(beekeeperId, beehiveId);

            case UserRole.ApiaryAdmin:
            {
                var beehive = await _uow.Beehives.GetByIdAsync(beehiveId);
                return beehive is not null && _user.ApiaryId == beehive.ApiaryId;
            }

            case UserRole.OrganizationAdmin:
            {
                var beehive = await _uow.Beehives.GetByIdAsync(beehiveId);
                if (beehive is null) return false;
                var apiary = await _uow.Apiaries.GetByIdAsync(beehive.ApiaryId);
                return apiary is not null && _user.OrganizationId == apiary.OrganizationId;
            }

            default:
                return false;
        }
    }

    // ── Assignment sets ───────────────────────────────────────────────────────────

    public async Task<HashSet<int>> GetAssignedBeehiveIdsAsync()
    {
        if (_user.UserId is not int userId) return [];
        return await _uow.Users.GetAssignedBeehiveIdsAsync(userId);
    }

    public async Task<HashSet<int>> GetAssignedApiaryIdsAsync()
    {
        if (_user.UserId is not int userId) return [];
        return await _uow.Users.GetAssignedApiaryIdsAsync(userId);
    }

    // ── Visible sets ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<Beehive>> GetAccessibleBeehivesAsync(bool includeLocked = false)
    {
        var beehives = await RoleScopedBeehivesAsync();
        if (includeLocked) return beehives;

        var locked = await _planLock.GetForCurrentUserAsync();
        return locked.BeehiveIds.Count == 0
            ? beehives
            : beehives.Where(b => !locked.BeehiveIds.Contains(b.Id)).ToList();
    }

    private async Task<IReadOnlyList<Beehive>> RoleScopedBeehivesAsync()
    {
        switch (_user.Role)
        {
            case UserRole.SystemAdmin:
                return (await _uow.Beehives.GetAllActiveAsync()).ToList();

            case UserRole.Beekeeper:
            {
                var assignedIds = await GetAssignedBeehiveIdsAsync();
                return assignedIds.Count > 0
                    ? (await _uow.Beehives.FindAsync(b =>
                        assignedIds.Contains(b.Id) && b.MergedIntoBeehiveId == null)).ToList()
                    : [];
            }

            case UserRole.ApiaryAdmin when _user.ApiaryId is int apiaryId:
                return (await _uow.Beehives.GetByApiaryIdAsync(apiaryId)).ToList();

            default:
                return _user.OrganizationId is int orgId
                    ? (await _uow.Beehives.GetByOrganizationAsync(orgId)).ToList()
                    : [];
        }
    }

    public async Task<IReadOnlyList<Apiary>> GetAccessibleApiariesAsync(bool includeLocked = false)
    {
        var apiaries = await RoleScopedApiariesAsync();
        if (includeLocked) return apiaries;

        var locked = await _planLock.GetForCurrentUserAsync();
        return locked.ApiaryIds.Count == 0
            ? apiaries
            : apiaries.Where(a => !locked.ApiaryIds.Contains(a.Id)).ToList();
    }

    private async Task<IReadOnlyList<Apiary>> RoleScopedApiariesAsync()
    {
        switch (_user.Role)
        {
            case UserRole.SystemAdmin:
                return (await _uow.Apiaries.GetAllAsync()).ToList();

            case UserRole.Beekeeper:
            {
                if (_user.OrganizationId is not int beekeeperOrgId) return [];
                var assignedApiaryIds = await GetAssignedApiaryIdsAsync();
                return assignedApiaryIds.Count == 0
                    ? []
                    : (await _uow.Apiaries.GetAllByOrganizationAsync(beekeeperOrgId))
                        .Where(a => assignedApiaryIds.Contains(a.Id))
                        .ToList();
            }

            case UserRole.ApiaryAdmin when _user.ApiaryId is int apiaryId:
            {
                var apiary = await _uow.Apiaries.GetByIdAsync(apiaryId);
                return apiary is null ? [] : [apiary];
            }

            default:
                return _user.OrganizationId is int orgId
                    ? (await _uow.Apiaries.GetAllByOrganizationAsync(orgId)).ToList()
                    : [];
        }
    }
}
