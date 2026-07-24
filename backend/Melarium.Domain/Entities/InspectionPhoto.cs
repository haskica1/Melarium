using Melarium.Domain.Common;

namespace Melarium.Domain.Entities;

/// <summary>
/// A photo attached to a beehive inspection (SPEC-05). The image bytes live in blob storage
/// (<c>StoragePath</c> is the storage key); this row carries the metadata and, after the
/// optional AI frame analysis, the raw analysis JSON.
/// </summary>
public class InspectionPhoto : BaseEntity
{
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>image/jpeg | image/png | image/webp — validated from real header bytes on upload.</summary>
    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string? Caption { get; set; }

    /// <summary>Raw JSON of the AI frame analysis (Phase 2); null until analyzed.</summary>
    public string? AnalysisJson { get; set; }

    // Foreign key
    public int InspectionId { get; set; }

    // Navigation property
    public Inspection Inspection { get; set; } = null!;
}
