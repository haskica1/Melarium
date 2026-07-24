using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>User-specific data access operations.</summary>
public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<IEnumerable<User>> GetAllWithOrganizationAsync();
    Task<User?> GetByIdWithOrganizationAsync(int id);
    Task<User?> GetByIdWithAssignedBeehivesAsync(int id);
    Task<bool> IsUserAssignedToBeehiveAsync(int userId, int beehiveId);
    Task SetBeehiveAssignmentsAsync(int userId, IEnumerable<int> beehiveIds);

    /// <summary>IDs of the beehives assigned to the user — no entities loaded.</summary>
    Task<HashSet<int>> GetAssignedBeehiveIdsAsync(int userId);

    /// <summary>IDs of the apiaries containing the user's assigned beehives — no entities loaded.</summary>
    Task<HashSet<int>> GetAssignedApiaryIdsAsync(int userId);

    // ── Recipient resolution for alerts (SPEC-04) ──

    /// <summary>User ids of every Beekeeper assigned to the given beehive.</summary>
    Task<List<int>> GetUserIdsAssignedToBeehiveAsync(int beehiveId);

    /// <summary>User ids of every Beekeeper assigned to at least one beehive in the given apiary.</summary>
    Task<List<int>> GetUserIdsAssignedToApiaryAsync(int apiaryId);

    /// <summary>User ids of the OrganizationAdmins of the given organization.</summary>
    Task<List<int>> GetOrganizationAdminIdsAsync(int organizationId);

    /// <summary>User ids of the ApiaryAdmins assigned to the given apiary.</summary>
    Task<List<int>> GetApiaryAdminIdsAsync(int apiaryId);

    /// <summary>Ids of every user on the platform — no entities loaded (SPEC-06 publish broadcast).</summary>
    Task<List<int>> GetAllIdsAsync();

    /// <summary>Number of user accounts in the organization — plan member-limit checks (SPEC-09).</summary>
    Task<int> CountByOrganizationAsync(int organizationId);
}
