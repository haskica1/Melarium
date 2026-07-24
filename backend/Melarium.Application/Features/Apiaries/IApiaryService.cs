using Melarium.Application.Features.Apiaries.DTOs;

namespace Melarium.Application.Features.Apiaries;

public interface IApiaryService
{
    /// <summary>Returns the apiaries visible to the current caller, scoped to their role.</summary>
    Task<IEnumerable<ApiaryDto>> GetAllForCurrentUserAsync();
    Task<ApiaryDetailDto> GetByIdAsync(int id);
    Task<ApiaryDto> CreateAsync(CreateApiaryDto dto);
    Task<ApiaryDto> UpdateAsync(int id, UpdateApiaryDto dto);
    Task DeleteAsync(int id);
}
