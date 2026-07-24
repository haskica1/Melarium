using System.Text.Json;
using AutoMapper;
using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Ai;
using Melarium.Application.Features.Inspections.DTOs;
using Melarium.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Melarium.Application.Features.Inspections;

public class InspectionPhotoService : IInspectionPhotoService
{
    public const int MaxPhotosPerInspection = 5;
    public const long MaxSizeBytes = 8 * 1024 * 1024;

    // Camel-case to match the API's JSON contract — the frontend parses this raw string.
    // Relaxed escaping keeps Bosnian characters (š, č, ž…) readable in the stored JSON
    // (VoiceParsingService precedent).
    private static readonly JsonSerializerOptions AnalysisJsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IAccessGuard _access;
    private readonly IFileStorage _storage;
    private readonly IPhotoAnalysisAiClient _vision;
    private readonly ICurrentUser _currentUser;
    private readonly IPlanGuard _plan;
    private readonly ILogger<InspectionPhotoService> _logger;

    public InspectionPhotoService(
        IUnitOfWork uow,
        IMapper mapper,
        IAccessGuard access,
        IFileStorage storage,
        IPhotoAnalysisAiClient vision,
        ICurrentUser currentUser,
        IPlanGuard plan,
        ILogger<InspectionPhotoService> logger)
    {
        _uow = uow;
        _mapper = mapper;
        _access = access;
        _storage = storage;
        _vision = vision;
        _currentUser = currentUser;
        _plan = plan;
        _logger = logger;
    }

    public async Task<InspectionPhotoDto> AddAsync(int inspectionId, Stream content, long sizeBytes, string? caption)
    {
        var inspection = await _uow.Inspections.GetByIdAsync(inspectionId)
            ?? throw new NotFoundException(nameof(Inspection), inspectionId);

        await _access.EnsureCanAccessBeehiveAsync(inspection.BeehiveId);

        if (sizeBytes > MaxSizeBytes)
            throw new BusinessRuleException(
                $"Fotografija ne smije biti veća od {MaxSizeBytes / (1024 * 1024)} MB.");

        var existing = await _uow.InspectionPhotos.CountByInspectionAsync(inspectionId);
        if (existing >= MaxPhotosPerInspection)
            throw new BusinessRuleException(
                $"Pregled može imati najviše {MaxPhotosPerInspection} fotografija.");

        // The real content type comes from the file's header bytes — the client-supplied
        // type and extension are untrusted.
        var (seekable, contentType) = await SniffContentTypeAsync(content);
        if (contentType is null)
            throw new BusinessRuleException("Dozvoljeni formati fotografija su JPEG, PNG i WebP.");

        var storagePath = await _storage.SaveAsync(seekable, contentType);

        var photo = new InspectionPhoto
        {
            InspectionId = inspectionId,
            StoragePath  = storagePath,
            ContentType  = contentType,
            SizeBytes    = sizeBytes,
            Caption      = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim(),
        };

        try
        {
            await _uow.InspectionPhotos.AddAsync(photo);
            await _uow.SaveChangesAsync();
        }
        catch
        {
            // The blob was already written — don't leave it orphaned if the DB insert fails.
            await TryDeleteBlobAsync(storagePath);
            throw;
        }

        return _mapper.Map<InspectionPhotoDto>(photo);
    }

    public async Task<IEnumerable<InspectionPhotoDto>> GetByInspectionAsync(int inspectionId)
    {
        var inspection = await _uow.Inspections.GetByIdAsync(inspectionId)
            ?? throw new NotFoundException(nameof(Inspection), inspectionId);

        await _access.EnsureCanAccessBeehiveAsync(inspection.BeehiveId);

        var photos = await _uow.InspectionPhotos.GetByInspectionIdAsync(inspectionId);
        return _mapper.Map<IEnumerable<InspectionPhotoDto>>(photos);
    }

    public async Task<(Stream Content, string ContentType)> OpenFileAsync(int photoId)
    {
        var photo = await GetPhotoWithAccessCheckAsync(photoId);

        try
        {
            var stream = await _storage.OpenReadAsync(photo.StoragePath);
            return (stream, photo.ContentType);
        }
        catch (FileNotFoundException)
        {
            // DB row exists but the blob is gone (e.g. wiped dev disk) — a clean 404 beats a 500.
            throw new NotFoundException(nameof(InspectionPhoto), photoId);
        }
    }

    public async Task DeleteAsync(int photoId)
    {
        var photo = await GetPhotoWithAccessCheckAsync(photoId);

        await _uow.InspectionPhotos.DeleteAsync(photo);
        await _uow.SaveChangesAsync();

        // Blob removal is best-effort — a storage hiccup must not undo the DB delete.
        await TryDeleteBlobAsync(photo.StoragePath);
    }

    public async Task<InspectionPhotoDto> AnalyzeAsync(int photoId)
    {
        // AI frame analysis is a Pro+ feature (SPEC-09); org-less callers pass through.
        if (_currentUser.OrganizationId is int orgId)
            await _plan.EnsureFeatureAsync(orgId, PlanFeature.PhotoAnalysis);

        var photo = await GetPhotoWithAccessCheckAsync(photoId);

        byte[] imageBytes;
        try
        {
            await using var stream = await _storage.OpenReadAsync(photo.StoragePath);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            imageBytes = buffer.ToArray();
        }
        catch (FileNotFoundException)
        {
            throw new NotFoundException(nameof(InspectionPhoto), photoId);
        }

        var result = await _vision.AnalyzeFrameAsync(imageBytes, photo.ContentType);

        // Re-analyze overwrites the previous result (SPEC-05).
        photo.AnalysisJson = JsonSerializer.Serialize(result, AnalysisJsonOpts);
        await _uow.InspectionPhotos.UpdateAsync(photo);
        await _uow.SaveChangesAsync();

        return _mapper.Map<InspectionPhotoDto>(photo);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<InspectionPhoto> GetPhotoWithAccessCheckAsync(int photoId)
    {
        var photo = await _uow.InspectionPhotos.GetByIdAsync(photoId)
            ?? throw new NotFoundException(nameof(InspectionPhoto), photoId);

        var inspection = await _uow.Inspections.GetByIdAsync(photo.InspectionId)
            ?? throw new NotFoundException(nameof(Inspection), photo.InspectionId);

        await _access.EnsureCanAccessBeehiveAsync(inspection.BeehiveId);
        return photo;
    }

    private async Task TryDeleteBlobAsync(string storagePath)
    {
        try
        {
            await _storage.DeleteAsync(storagePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete stored file {StoragePath} — orphaned blob left behind", storagePath);
        }
    }

    /// <summary>
    /// Determines the image type from magic bytes (JPEG / PNG / WebP), returning a seekable
    /// stream positioned at 0 alongside it (non-seekable inputs are buffered).
    /// </summary>
    private static async Task<(Stream Stream, string? ContentType)> SniffContentTypeAsync(Stream content)
    {
        var stream = content;
        if (!stream.CanSeek)
        {
            var buffered = new MemoryStream();
            await content.CopyToAsync(buffered);
            stream = buffered;
        }

        stream.Position = 0;
        var header = new byte[12];
        var read = 0;
        while (read < header.Length)
        {
            var n = await stream.ReadAsync(header.AsMemory(read, header.Length - read));
            if (n == 0) break;
            read += n;
        }
        stream.Position = 0;

        return (stream, DetectContentType(header, read));
    }

    private static string? DetectContentType(byte[] header, int length)
    {
        if (length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return "image/jpeg";

        if (length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return "image/png";

        // RIFF....WEBP
        if (length >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return "image/webp";

        return null;
    }
}
