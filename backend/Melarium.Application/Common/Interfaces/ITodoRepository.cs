using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Todo-specific data access operations.</summary>
public interface ITodoRepository : IRepository<Todo>
{
    Task<IEnumerable<Todo>> GetByApiaryIdAsync(int apiaryId);
    Task<IEnumerable<Todo>> GetByBeehiveIdAsync(int beehiveId);
    Task<Todo?> GetByIdWithUsersAsync(int id);

    /// <summary>Returns all open todos across the entire organization (apiary- and beehive-level).</summary>
    Task<IEnumerable<Todo>> GetAllOpenByOrganizationAsync(int organizationId);

    /// <summary>Returns all open todos attached to a specific apiary or any of its beehives.</summary>
    Task<IEnumerable<Todo>> GetAllOpenByApiaryAsync(int apiaryId);
}
