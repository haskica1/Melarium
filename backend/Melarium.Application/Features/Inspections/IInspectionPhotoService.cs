using Melarium.Application.Features.Inspections.DTOs;

namespace Melarium.Application.Features.Inspections;

/// <summary>Photo attachments on inspections (SPEC-05). Access mirrors the parent inspection.</summary>
public interface IInspectionPhotoService
{
    /// <summary>Validates (count/size/real content type) and stores a new photo for the inspection.</summary>
    Task<InspectionPhotoDto> AddAsync(int inspectionId, Stream content, long sizeBytes, string? caption);

    Task<IEnumerable<InspectionPhotoDto>> GetByInspectionAsync(int inspectionId);

    /// <summary>Opens the stored image for streaming to the client (auth-checked).</summary>
    Task<(Stream Content, string ContentType)> OpenFileAsync(int photoId);

    /// <summary>Deletes the photo row and its blob (blob best-effort).</summary>
    Task DeleteAsync(int photoId);

    /// <summary>
    /// Runs the AI frame analysis (Phase 2), persists the result JSON on the photo and
    /// returns the updated DTO. Re-analyzing overwrites the previous result.
    /// </summary>
    Task<InspectionPhotoDto> AnalyzeAsync(int photoId);
}
