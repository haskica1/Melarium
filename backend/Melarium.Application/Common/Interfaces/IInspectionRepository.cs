using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Inspection-specific data access operations.</summary>
public interface IInspectionRepository : IRepository<Inspection>
{
    /// <summary>Returns all inspections for a given beehive, ordered by date descending.</summary>
    Task<IEnumerable<Inspection>> GetByBeehiveIdAsync(int beehiveId);

    /// <summary>Inspection counts per beehive for one apiary, grouped in the database.</summary>
    Task<Dictionary<int, int>> CountByBeehiveForApiaryAsync(int apiaryId);

    /// <summary>Newest inspection date per hive — one grouped query; hives never inspected are absent.</summary>
    Task<Dictionary<int, DateTime>> GetLastDatesAsync(IReadOnlyCollection<int> beehiveIds);

    /// <summary>Hive, date and honey level of every inspection since <paramref name="since"/>, newest first (SPEC-29 dashboard).</summary>
    Task<List<Domain.Common.InspectionLevelInfo>> GetLevelsSinceAsync(IReadOnlyCollection<int> beehiveIds, DateTime since);
}
