using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Colony merge (sastavljanje društava) history data access (SPEC-19).</summary>
public interface IBeehiveMergeRepository : IRepository<BeehiveMerge>
{
    /// <summary>The merge with both hives and the author loaded. Finds undone rows too.</summary>
    Task<BeehiveMerge?> GetWithHivesAsync(int id);

    /// <summary>
    /// Merges this hive <b>received</b> (it is the target), in force only, newest first.
    /// </summary>
    Task<IEnumerable<BeehiveMerge>> GetReceivedByBeehiveAsync(int beehiveId);

    /// <summary>The in-force merge that took this hive out of its apiary, or null.</summary>
    Task<BeehiveMerge?> GetActiveBySourceAsync(int sourceBeehiveId);

    /// <summary>
    /// In-force merges received by any of the given hives — one query for a hive list.
    /// </summary>
    Task<IEnumerable<BeehiveMerge>> GetReceivedByBeehivesAsync(IReadOnlyCollection<int> beehiveIds);
}
