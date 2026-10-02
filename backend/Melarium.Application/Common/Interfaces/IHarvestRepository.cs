using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Common.Interfaces;

/// <summary>
/// Harvest data access — honey and, since SPEC-30, every other bee product. Every method that can feed
/// a sum takes a <see cref="HarvestKind"/> without a default, so each caller has to say whether it reads
/// honey or the other products; a kilo of wax must never land in "kg meda".
/// </summary>
public interface IHarvestRepository : IRepository<Harvest>
{
    /// <summary>Every record of an organization — shared ones and every apiary's — newest first, entries + apiary loaded.</summary>
    Task<IEnumerable<Harvest>> GetByOrganizationAsync(int organizationId, HarvestKind kind, int? year = null);

    /// <summary>One apiary's records, newest first, entries loaded. Shared records are not included.</summary>
    Task<IEnumerable<Harvest>> GetByApiaryAsync(int apiaryId, HarvestKind kind, int? year = null);

    /// <summary>Records of a set of apiaries (entries + apiary loaded), newest first — the aggregation input of stats, the dashboard and the report.</summary>
    Task<IEnumerable<Harvest>> GetByApiariesAsync(IReadOnlyCollection<int> apiaryIds, HarvestKind kind, int? year = null);

    /// <summary>
    /// Shared records (no apiary) of one organization, or of every organization when
    /// <paramref name="organizationId"/> is null — the platform-wide stats of the org-less SystemAdmin.
    /// </summary>
    Task<IEnumerable<Harvest>> GetSharedAsync(int? organizationId, HarvestKind kind, int? year = null);

    /// <summary>Records that include a given hive, newest first; entries + apiary loaded.</summary>
    Task<IEnumerable<Harvest>> GetByBeehiveAsync(int beehiveId, HarvestKind kind);

    /// <summary>A single record with its entries (incl. beehive names), apiary and author eagerly loaded.</summary>
    Task<Harvest?> GetWithEntriesAsync(int id);

    /// <summary>
    /// Total kg per beehive for the given hives (optionally a single year), computed in the database —
    /// no rows materialized. Only per-hive lines count; only hives with a positive total appear.
    /// </summary>
    Task<Dictionary<int, decimal>> GetHiveTotalsAsync(IReadOnlyCollection<int> beehiveIds, HarvestKind kind, int? year = null);

    /// <summary>
    /// A hive's kg per (year, product), computed in the database. Only per-hive lines count: a record
    /// kept as one figure for the apiary or the organization cannot be attributed to any one hive.
    /// </summary>
    Task<Dictionary<(int Year, HiveProductType ProductType), decimal>> GetHiveTotalsByYearAsync(int beehiveId);
}
