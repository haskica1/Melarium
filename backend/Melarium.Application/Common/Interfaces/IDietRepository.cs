using Melarium.Domain.Common;
using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>
/// Diet-specific data access operations. Naming deliberately mirrors <see cref="ITreatmentRepository"/>
/// — since SPEC-12 the two features have the same shape (apiary-scoped parent, per-hive link rows).
/// </summary>
public interface IDietRepository : IRepository<Diet>
{
    Task<IEnumerable<Diet>> GetByApiaryAsync(int apiaryId, int? year = null);

    Task<IEnumerable<Diet>> GetByOrganizationAsync(int organizationId, int? year = null);

    /// <summary>Programmes that currently cover this hive, plus those it was removed from (history).</summary>
    Task<IEnumerable<Diet>> GetByBeehiveAsync(int beehiveId);

    /// <summary>Calendar path. Includes the hive links so callers can narrow to a Beekeeper's own hives.</summary>
    Task<IEnumerable<Diet>> GetByApiaryIdsAsync(IEnumerable<int> apiaryIds);

    Task<Diet?> GetWithEntriesAsync(int id);

    /// <summary>
    /// Active programmes per hive. A list, not a single value: overlapping programmes are allowed,
    /// so a hive can legitimately be on a stimulative feeding and a protein supplement at once.
    /// </summary>
    Task<Dictionary<int, List<DietActiveInfo>>> GetActiveForBeehivesAsync(IReadOnlyCollection<int> beehiveIds);
}
