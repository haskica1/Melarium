namespace Melarium.Application.Features.Treatments.DTOs;

/// <summary>
/// Body for ticking a treatment round. Ticking without a note is the normal case, so an empty body
/// is valid — the note is the escape hatch for an exception worth recording.
/// </summary>
public class CompleteTreatmentRoundDto
{
    public string? Note { get; set; }
}
