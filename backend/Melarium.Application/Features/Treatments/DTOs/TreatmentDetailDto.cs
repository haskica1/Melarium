namespace Melarium.Application.Features.Treatments.DTOs;

/// <summary>Full treatment including its per-hive entries.</summary>
public class TreatmentDetailDto : TreatmentDto
{
    public List<TreatmentEntryDto> Entries { get; set; } = [];
    public List<TreatmentRoundDto> Rounds { get; set; } = [];
}
