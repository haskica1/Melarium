using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Inspection-specific data access operations.</summary>
public interface IInspectionRepository : IRepository<Inspection>
{
    /// <summary>Returns all inspections for a given beehive, ordered by date descending.</summary>
    Task<IEnumerable<Inspection>> GetByBeehiveIdAsync(int beehiveId);

    /// <summary>Inspection counts per beehive for one apiary, grouped in the database.</summary>
    Task<Dictionary<int, int>> CountByBeehiveForApiaryAsync(int apiaryId);
}
