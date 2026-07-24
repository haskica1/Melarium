namespace Melarium.Application.Features.Inspections.DTOs;

/// <summary>Form fields accompanying a photo upload (the file itself arrives as multipart content).</summary>
public record UploadInspectionPhotoDto(string? Caption);
