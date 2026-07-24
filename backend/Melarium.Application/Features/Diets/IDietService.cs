using Melarium.Application.Features.Diets.DTOs;

namespace Melarium.Application.Features.Diets;

public interface IDietService
{
    Task<IEnumerable<DietDto>> GetByBeehiveIdAsync(int beehiveId);
    Task<DietDetailDto> GetByIdAsync(int id);
    Task<DietDetailDto> CreateAsync(CreateDietDto dto);
    Task<IEnumerable<DietDto>> CopyToBeehivesAsync(int sourceDietId, CopyDietDto dto);
    Task<DietDetailDto> UpdateAsync(int id, UpdateDietDto dto);
    Task DeleteAsync(int id);
    Task<DietDetailDto> CompleteEarlyAsync(int id, CompleteEarlyDto dto);
    Task<FeedingEntryDto> CompleteFeedingEntryAsync(int dietId, int entryId);
}
