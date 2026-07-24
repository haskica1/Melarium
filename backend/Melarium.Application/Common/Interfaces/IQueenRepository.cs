using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Queen-specific data access operations.</summary>
public interface IQueenRepository : IRepository<Queen>
{
    /// <summary>Full queen history for a beehive, newest introduction first.</summary>
    Task<IEnumerable<Queen>> GetByBeehiveIdAsync(int beehiveId);

    /// <summary>The currently active queen of a beehive (tracked), or null.</summary>
    Task<Queen?> GetActiveByBeehiveIdAsync(int beehiveId);

    /// <summary>Active queens for a set of beehives, keyed by beehive id (read-only projection).</summary>
    Task<Dictionary<int, Queen>> GetActiveByBeehiveIdsAsync(IReadOnlyCollection<int> beehiveIds);
}
