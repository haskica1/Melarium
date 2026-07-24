using Melarium.Application.Features.Inspections.DTOs;

namespace Melarium.Application.Features.Inspections;

public interface IInspectionService
{
    Task<IEnumerable<InspectionDto>> GetByBeehiveIdAsync(int beehiveId);
    Task<InspectionDto> GetByIdAsync(int id);
    Task<InspectionDto> CreateAsync(CreateInspectionDto dto);
    Task<InspectionDto> UpdateAsync(int id, UpdateInspectionDto dto);
    Task DeleteAsync(int id);
}
