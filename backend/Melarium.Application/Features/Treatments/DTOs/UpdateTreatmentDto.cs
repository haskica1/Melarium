using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Treatments.DTOs;

/// <summary>Update payload — the apiary is immutable after creation (entries belong to its hives).</summary>
public class UpdateTreatmentDto
{
    public TreatmentPurpose Purpose { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public ActiveSubstance ActiveSubstance { get; set; }
    public ApplicationMethod Method { get; set; }
    public string DosePerHive { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int WithdrawalDays { get; set; }
    public string? BatchNumber { get; set; }
    public string? Supplier { get; set; }
    public string? Notes { get; set; }
    public List<CreateTreatmentEntryDto> Entries { get; set; } = [];
}
