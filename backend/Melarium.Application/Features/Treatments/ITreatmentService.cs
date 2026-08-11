using Melarium.Application.Features.Treatments.DTOs;

namespace Melarium.Application.Features.Treatments;

public interface ITreatmentService
{
    /// <summary>Role-scoped list, optionally filtered by apiary, hive, and/or year.</summary>
    Task<IEnumerable<TreatmentDto>> GetAllAsync(int? apiaryId, int? beehiveId, int? year);

    Task<TreatmentDetailDto> GetByIdAsync(int id);
    Task<TreatmentDetailDto> CreateAsync(CreateTreatmentDto dto);
    Task<TreatmentDetailDto> UpdateAsync(int id, UpdateTreatmentDto dto);
    Task DeleteAsync(int id);

    /// <summary>Ticks one scheduled application round. Never touches the legal StartDate/EndDate record.</summary>
    Task<TreatmentRoundDto> CompleteTreatmentRoundAsync(int treatmentId, int roundId, CompleteTreatmentRoundDto dto);
}
