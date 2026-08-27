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

    public AccessGuard(ICurrentUser user, IUnitOfWork uow)
    {
        _user = user;
        _uow = uow;
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

    public async Task EnsureCanManageApiaryAsync(int apiaryId)
    {
        if (_user.Role == UserRole.SystemAdmin) return;

        // An ApiaryAdmin is bound to a single apiary id, so no lookup is needed.
        if (_user.Role == UserRole.ApiaryAdmin)
        {
            if (_user.ApiaryId == apiaryId) return;
            throw new ForbiddenAccessException();
        }

        var apiary = await _uow.Apiaries.GetByIdAsync(apiaryId)
            ?? throw new ForbiddenAccessException();
        EnsureCanManageApiary(apiaryId, apiary.OrganizationId);
    }

    public async Task<bool> CanManageApiaryAsync(int apiaryId)
    {
        try
        {
            await EnsureCanManageApiaryAsync(apiaryId);
            return true;
        }
        catch (ForbiddenAccessException)
        {
            return false;
        }
    }

    public async Task EnsureCanAccessBeehiveAsync(int beehiveId)
    {
        if (!await CanAccessBeehiveAsync(beehiveId))
            throw new ForbiddenAccessException();
    }

    public async Task<bool> CanAccessBeehiveAsync(int beehiveId)
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

    public async Task<IReadOnlyList<Beehive>> GetAccessibleBeehivesAsync()
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

    public async Task<IReadOnlyList<Apiary>> GetAccessibleApiariesAsync()
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
