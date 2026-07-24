using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Field-level edit history for queen records (SPEC-03).</summary>
public interface IQueenEditLogRepository : IRepository<QueenEditLog>
{
    /// <summary>A queen's edit log, newest first; author loaded.</summary>
    Task<IEnumerable<QueenEditLog>> GetByQueenIdAsync(int queenId);
}
