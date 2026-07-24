using Melarium.Application.Features.Inspections;
using Melarium.Application.Features.Inspections.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Melarium.API.Controllers;

/// <summary>
/// Records and manages beehive inspections (pregledi). Access to a beehive's inspections is
/// enforced in the service layer (managers within scope, or a Beekeeper assigned to the hive).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class InspectionsController : ControllerBase
{
    private readonly IInspectionService _service;
    private readonly IInspectionPhotoService _photoService;
    private readonly IVoiceParsingService _voiceParsingService;
    private readonly IValidator<CreateInspectionDto> _createValidator;
    private readonly IValidator<UpdateInspectionDto> _updateValidator;
    private readonly IValidator<UploadInspectionPhotoDto> _uploadPhotoValidator;

    public InspectionsController(
        IInspectionService service,
        IInspectionPhotoService photoService,
        IVoiceParsingService voiceParsingService,
        IValidator<CreateInspectionDto> createValidator,
        IValidator<UpdateInspectionDto> updateValidator,
        IValidator<UploadInspectionPhotoDto> uploadPhotoValidator)
    {
        _service              = service;
        _photoService         = photoService;
        _voiceParsingService  = voiceParsingService;
        _createValidator      = createValidator;
        _updateValidator      = updateValidator;
        _uploadPhotoValidator = uploadPhotoValidator;
    }

    /// <summary>Returns all inspections for the specified beehive, newest first.</summary>
    [HttpGet("by-beehive/{beehiveId:int}")]
    [ProducesResponseType(typeof(IEnumerable<InspectionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByBeehive(int beehiveId)
    {
        var inspections = await _service.GetByBeehiveIdAsync(beehiveId);
        return Ok(inspections);
    }

    /// <summary>Returns a single inspection by ID.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(InspectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var inspection = await _service.GetByIdAsync(id);
        return Ok(inspection);
    }

    /// <summary>Records a new inspection for a beehive the caller can access.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(InspectionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateInspectionDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates an existing inspection record.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(InspectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateInspectionDto dto)
    {
        var validation = await _updateValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var updated = await _service.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Deletes an inspection record.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // ── Photos (SPEC-05) ──────────────────────────────────────────────────────

    /// <summary>
    /// Attaches a photo to an inspection. Max 5 photos per inspection, max 8 MB each;
    /// JPEG/PNG/WebP only (validated from real header bytes). EXIF metadata is preserved.
    /// </summary>
    [HttpPost("{id:int}/photos")]
    [Consumes("multipart/form-data")]
    // 8 MB photo cap + multipart/form overhead.
    [RequestSizeLimit(9_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 9_000_000)]
    [ProducesResponseType(typeof(InspectionPhotoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadPhoto(int id, IFormFile? file, [FromForm] string? caption)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Fotografija je obavezna." });

        var validation = await _uploadPhotoValidator.ValidateAsync(new UploadInspectionPhotoDto(caption));
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        await using var stream = file.OpenReadStream();
        var created = await _photoService.AddAsync(id, stream, file.Length, caption);
        return CreatedAtAction(nameof(GetPhotoFile), new { photoId = created.Id }, created);
    }

    /// <summary>Returns photo metadata for an inspection (image bytes are streamed per photo).</summary>
    [HttpGet("{id:int}/photos")]
    [ProducesResponseType(typeof(IEnumerable<InspectionPhotoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPhotos(int id)
    {
        var photos = await _photoService.GetByInspectionAsync(id);
        return Ok(photos);
    }

    /// <summary>Streams the photo image. Auth-checked — the storage bucket is never public.</summary>
    [HttpGet("photos/{photoId:int}/file")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPhotoFile(int photoId)
    {
        var (content, contentType) = await _photoService.OpenFileAsync(photoId);
        Response.Headers.CacheControl = "private, max-age=86400";
        return File(content, contentType);
    }

    /// <summary>
    /// Runs the AI frame analysis on a photo (SPEC-05 Phase 2). Persists and returns the
    /// result; re-running overwrites the previous analysis. Paid Groq call → rate limited.
    /// </summary>
    [HttpPost("photos/{photoId:int}/analyze")]
    [EnableRateLimiting("photo-analyze")]
    [ProducesResponseType(typeof(InspectionPhotoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> AnalyzePhoto(int photoId)
    {
        var updated = await _photoService.AnalyzeAsync(photoId);
        return Ok(updated);
    }

    /// <summary>Deletes a photo (row + stored file).</summary>
    [HttpDelete("photos/{photoId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePhoto(int photoId)
    {
        await _photoService.DeleteAsync(photoId);
        return NoContent();
    }

    /// <summary>
    /// Accepts a recorded audio file, transcribes it, then extracts inspection field values.
    /// Returns transcript + extracted fields (null for anything not mentioned).
    /// </summary>
    [HttpPost("parse-voice")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("voice-parse")]
    // Voice notes are short recordings — 15 MB is generous. Without a cap any authenticated
    // user could push the Kestrel default (~30 MB) per request straight to the paid Groq API.
    [RequestSizeLimit(15_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 15_000_000)]
    [ProducesResponseType(typeof(ParseVoiceResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ParseVoice(IFormFile? audio)
    {
        if (audio == null || audio.Length == 0)
            return BadRequest(new { message = "Audio file is required." });

        await using var stream = audio.OpenReadStream();
        var result = await _voiceParsingService.ParseAsync(stream, audio.FileName);
        return Ok(result);
    }
}
