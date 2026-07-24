namespace Melarium.Application.Features.Inspections.DTOs;

/// <summary>Metadata of a photo attached to an inspection — image bytes are streamed separately.</summary>
public class InspectionPhotoDto
{
    public int Id { get; set; }
    public int InspectionId { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? Caption { get; set; }

    /// <summary>Raw AI frame-analysis JSON (Phase 2); null until the photo is analyzed.</summary>
    public string? AnalysisJson { get; set; }

    public DateTime CreatedAt { get; set; }
}
