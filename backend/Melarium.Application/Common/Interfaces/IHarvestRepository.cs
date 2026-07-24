using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Harvest-specific data access operations.</summary>
public interface IHarvestRepository : IRepository<Harvest>
{
    /// <summary>All harvests for an organization (across its apiaries), newest first, entries + apiary loaded.</summary>
    Task<IEnumerable<Harvest>> GetByOrganizationAsync(int organizationId, int? year = null);

    /// <summary>All harvests for a single apiary, newest first, entries loaded.</summary>
    Task<IEnumerable<Harvest>> GetByApiaryAsync(int apiaryId, int? year = null);

    /// <summary>All harvests across a set of apiaries (with entries + apiary), newest first — used by stats.</summary>
    Task<IEnumerable<Harvest>> GetByApiariesAsync(IReadOnlyCollection<int> apiaryIds, int? year = null);

    /// <summary>All harvests that include a given hive, newest first; entries + apiary loaded.</summary>
    Task<IEnumerable<Harvest>> GetByBeehiveAsync(int beehiveId);

    /// <summary>A single harvest with its entries (incl. beehive names) and apiary eagerly loaded.</summary>
    Task<Harvest?> GetWithEntriesAsync(int id);

    /// <summary>
    /// Total extracted kg per beehive for the given hives (optionally a single year), computed in the
    /// database — no harvest/entry rows materialized. Only hives with a positive total appear.
    /// </summary>
    Task<Dictionary<int, decimal>> GetHiveTotalsAsync(IReadOnlyCollection<int> beehiveIds, int? year = null);

    /// <summary>Extracted kg for a single hive grouped by year (year → kg), computed in the database.</summary>
    Task<Dictionary<int, decimal>> GetHiveYearlyTotalsAsync(int beehiveId);
}
