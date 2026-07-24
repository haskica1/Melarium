namespace Melarium.Application.Features.Treatments.DTOs;

public class TreatmentEntryDto
{
    public int Id { get; set; }
    public int BeehiveId { get; set; }
    public string? BeehiveName { get; set; }
    public string? DoseNote { get; set; }
}
