using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Diet-specific data access operations.</summary>
public interface IDietRepository : IRepository<Diet>
{
    Task<IEnumerable<Diet>> GetByBeehiveIdAsync(int beehiveId);
    Task<IEnumerable<Diet>> GetByBeehiveIdsAsync(IEnumerable<int> beehiveIds);
    Task<Diet?> GetWithEntriesAsync(int id);
}
