using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>
/// Beehive-specific data access operations.
///
/// <para><b>Merged hives (SPEC-19 §5).</b> Every method here that returns a <i>list</i> excludes hives
/// whose colony was merged away (<see cref="Beehive.MergedIntoBeehiveId"/> set) — those are out of the
/// apiary for good. Single-hive lookups (<see cref="GetByIdAsync"/>, <see cref="GetWithInspectionsAsync"/>,
/// <see cref="GetByUniqueIdAsync"/>) deliberately still find them, so their history stays readable by id
/// and an old QR sticker resolves instead of 404-ing. The archive uses
/// <see cref="GetMergedByApiaryIdAsync"/>.</para>
/// </summary>
public interface IBeehiveRepository : IRepository<Beehive>
{
    /// <summary>Returns the beehive with its inspections eagerly loaded. Finds merged hives too.</summary>
    Task<Beehive?> GetWithInspectionsAsync(int id);

    /// <summary>Returns the apiary's beehives that are still in service.</summary>
    Task<IEnumerable<Beehive>> GetByApiaryIdAsync(int apiaryId);

    /// <summary>Returns the organization's in-service beehives (across all its apiaries).</summary>
    Task<IEnumerable<Beehive>> GetByOrganizationAsync(int organizationId);

    /// <summary>Every in-service beehive in the database — SystemAdmin scope (SPEC-19 §5 #4/#6/#8).</summary>
    Task<IEnumerable<Beehive>> GetAllActiveAsync();

    /// <summary>
    /// The archive: hives merged away, newest merge first. `apiaryId` null = across the organization.
    /// The only list method that returns merged hives.
    /// </summary>
    Task<IEnumerable<Beehive>> GetMergedByApiaryIdAsync(int apiaryId);

    /// <summary>Looks up a beehive by its permanent unique scan identifier.</summary>
    Task<Beehive?> GetByUniqueIdAsync(Guid uniqueId);

    /// <summary>Returns all beehives that have a UniqueId set (for QR regeneration).</summary>
    Task<IEnumerable<Beehive>> GetAllWithUniqueIdAsync();

    /// <summary>
    /// Number of in-service beehives across the organization's apiaries — plan limit checks (SPEC-09).
    /// A merged-away hive frees its slot: the colony genuinely no longer exists (SPEC-19 §1).
    /// </summary>
    Task<int> CountByOrganizationAsync(int organizationId);
}
