using Melarium.Application.Features.Queens.DTOs;

namespace Melarium.Application.Features.Queens;

public interface IQueenService
{
    Task<IEnumerable<QueenDto>> GetByBeehiveIdAsync(int beehiveId);
    Task<QueenDto> CreateAsync(int beehiveId, CreateQueenDto dto);
    Task<QueenDto> UpdateAsync(int id, UpdateQueenDto dto);
    Task DeleteAsync(int id);

    /// <summary>Field-level edit history for a single queen record, newest first.</summary>
    Task<IEnumerable<QueenEditLogDto>> GetEditHistoryAsync(int queenId);
}
