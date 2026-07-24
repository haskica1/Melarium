using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Pasture (pašnjak) registry data access (SPEC-10).</summary>
public interface IPastureRepository : IRepository<Pasture>
{
    /// <summary>All pastures of an organization, alphabetically, with the count of apiaries currently on each.</summary>
    Task<IEnumerable<(Pasture Pasture, int ApiariesOnPasture)>> GetByOrganizationWithCountsAsync(int organizationId);

    /// <summary>True when any apiary currently sits on the pasture or any move references it (delete guard).</summary>
    Task<bool> HasReferencesAsync(int pastureId);
}
