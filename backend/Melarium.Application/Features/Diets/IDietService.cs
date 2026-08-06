using Melarium.Application.Features.Diets.DTOs;

namespace Melarium.Application.Features.Diets;

public interface IDietService
{
    Task<IEnumerable<DietDto>> GetAllAsync(int? apiaryId, int? beehiveId, int? year);

    /// <summary>Active programmes for every hive of an apiary — one flat row per (hive × programme).</summary>
    Task<IEnumerable<DietActiveInfoDto>> GetActiveAsync(int apiaryId);

    Task<DietDetailDto> GetByIdAsync(int id);
    Task<DietDetailDto> CreateAsync(CreateDietDto dto);
    Task<DietDetailDto> UpdateAsync(int id, UpdateDietDto dto);
    Task DeleteAsync(int id);

    Task<DietDetailDto> AddBeehivesAsync(int id, AddDietBeehivesDto dto);

    /// <summary>Soft-removes the hive (sets RemovedOn) so the record of it having been fed survives.</summary>
    Task<DietDetailDto> RemoveBeehiveAsync(int id, int beehiveId);

    Task<DietDetailDto> CompleteEarlyAsync(int id, CompleteEarlyDto dto);
    Task<FeedingEntryDto> CompleteFeedingEntryAsync(int dietId, int entryId, CompleteFeedingEntryDto dto);
}
