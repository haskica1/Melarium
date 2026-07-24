using Melarium.Application.Features.Pastures.DTOs;

namespace Melarium.Application.Features.Pastures;

public interface IPastureService
{
    /// <summary>The caller's organization's pastures (SystemAdmin has no org → empty).</summary>
    Task<IEnumerable<PastureDto>> GetAllAsync();

    Task<PastureDto> CreateAsync(SavePastureDto dto);
    Task<PastureDto> UpdateAsync(int id, SavePastureDto dto);

    /// <summary>Blocked (400) while any apiary sits on the pasture or any move references it.</summary>
    Task DeleteAsync(int id);
}
