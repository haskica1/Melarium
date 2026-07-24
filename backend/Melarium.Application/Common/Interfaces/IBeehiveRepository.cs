using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Beehive-specific data access operations.</summary>
public interface IBeehiveRepository : IRepository<Beehive>
{
    /// <summary>Returns the beehive with its inspections eagerly loaded.</summary>
    Task<Beehive?> GetWithInspectionsAsync(int id);

    /// <summary>Returns all beehives belonging to a specific apiary.</summary>
    Task<IEnumerable<Beehive>> GetByApiaryIdAsync(int apiaryId);

    /// <summary>Returns all beehives belonging to a specific organization (across all its apiaries).</summary>
    Task<IEnumerable<Beehive>> GetByOrganizationAsync(int organizationId);

    /// <summary>Looks up a beehive by its permanent unique scan identifier.</summary>
    Task<Beehive?> GetByUniqueIdAsync(Guid uniqueId);

    /// <summary>Returns all beehives that have a UniqueId set (for QR regeneration).</summary>
    Task<IEnumerable<Beehive>> GetAllWithUniqueIdAsync();

    /// <summary>Number of beehives across the organization's apiaries — plan limit checks (SPEC-09).</summary>
    Task<int> CountByOrganizationAsync(int organizationId);
}
