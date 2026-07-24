using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Apiary relocation (selidba) history data access (SPEC-10).</summary>
public interface IApiaryMoveRepository : IRepository<ApiaryMove>
{
    /// <summary>An apiary's moves, newest first; pastures + author loaded.</summary>
    Task<IEnumerable<ApiaryMove>> GetByApiaryAsync(int apiaryId);

    /// <summary>The apiary's most recent move (by MovedAt, then CreatedAt), or null when it never moved.</summary>
    Task<ApiaryMove?> GetLatestForApiaryAsync(int apiaryId);

    /// <summary>Every move of the given apiaries (ToPasture loaded) — stats yield attribution, one query.</summary>
    Task<IEnumerable<ApiaryMove>> GetByApiariesAsync(IReadOnlyCollection<int> apiaryIds);
}
