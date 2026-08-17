using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Organization-specific data access operations.</summary>
public interface IOrganizationRepository : IRepository<Organization>
{
    Task<IEnumerable<Organization>> GetAllWithDetailsAsync();
    Task<Organization?> GetWithDetailsAsync(int id);

    /// <summary>
    /// Beehive count per organization, resolved through the owning apiary. Organizations without a
    /// single hive are absent from the dictionary rather than present with 0.
    /// </summary>
    Task<Dictionary<int, int>> GetBeehiveCountsAsync(int? organizationId = null);

    /// <summary>
    /// Newest "someone used this organization" moment per organization, computed from the records
    /// themselves — no stored column, so it is correct retroactively for data that already exists.
    /// Organizations that have never shown a sign of life are absent from the dictionary.
    /// </summary>
    Task<Dictionary<int, DateTime>> GetLastActivityAsync(int? organizationId = null);
}
