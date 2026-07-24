using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Apiary-specific data access operations.</summary>
public interface IApiaryRepository : IRepository<Apiary>
{
    /// <summary>Returns the apiary with its beehives eagerly loaded (no inspection rows).</summary>
    Task<Apiary?> GetWithBeehivesAsync(int id);

    /// <summary>Returns all apiaries belonging to a specific organization (no navigations loaded).</summary>
    Task<IEnumerable<Apiary>> GetAllByOrganizationAsync(int organizationId);

    /// <summary>
    /// Returns the organization's apiaries (with CreatedBy) plus their beehive counts,
    /// computed in the database instead of loading the beehive rows.
    /// </summary>
    Task<IReadOnlyList<(Apiary Apiary, int BeehiveCount)>> GetByOrganizationWithCountsAsync(int organizationId);

    /// <summary>Number of apiaries in the organization — plan limit checks (SPEC-09).</summary>
    Task<int> CountByOrganizationAsync(int organizationId);
}
