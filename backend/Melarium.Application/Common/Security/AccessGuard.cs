using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
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
}
