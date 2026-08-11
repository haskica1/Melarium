using Melarium.Domain.Common;
using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Treatment (veterinary medicine record) data access.</summary>
public interface ITreatmentRepository : IRepository<Treatment>
{
    /// <summary>All treatments for an organization (across its apiaries), newest first; entries + apiary loaded.</summary>
    Task<IEnumerable<Treatment>> GetByOrganizationAsync(int organizationId, int? year = null);

    /// <summary>All treatments for a single apiary, newest first; entries + apiary loaded.</summary>
    Task<IEnumerable<Treatment>> GetByApiaryAsync(int apiaryId, int? year = null);

    /// <summary>All treatments that include a given hive, newest first; entries + apiary loaded.</summary>
    Task<IEnumerable<Treatment>> GetByBeehiveAsync(int beehiveId);

    /// <summary>Calendar path. Includes rounds so callers can build the day-by-day obligation list.</summary>
    Task<IEnumerable<Treatment>> GetByApiaryIdsAsync(IEnumerable<int> apiaryIds);

    /// <summary>A single treatment with entries (incl. hive names) and apiary — tracked for edits.</summary>
    Task<Treatment?> GetWithEntriesAsync(int id);

    /// <summary>The latest treatment per hive for the given hives (raw; caller derives status).</summary>
    Task<Dictionary<int, TreatmentLatestInfo>> GetLatestForBeehivesAsync(IReadOnlyCollection<int> beehiveIds);
}
