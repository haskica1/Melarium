using Melarium.Application.Common.Security;
using Melarium.Application.Features.Beehives;
using Melarium.Application.Features.Beehives.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Melarium.API.Controllers;

/// <summary>
/// Manages beehive (košnica) resources. Role-based ownership is enforced in the service layer;
/// the controller only performs input validation and coarse role gating.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class BeehivesController : ControllerBase
{
    private readonly IBeehiveService _service;
    private readonly IValidator<CreateBeehiveDto> _createValidator;
    private readonly IValidator<UpdateBeehiveDto> _updateValidator;

    public BeehivesController(
        IBeehiveService service,
        IValidator<CreateBeehiveDto> createValidator,
        IValidator<UpdateBeehiveDto> updateValidator)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>
    /// Public QR scan lookup — resolves a beehive's unique scan ID to its internal ID and name.
    /// No authentication required; used as the first step of the scan-to-detail flow.
    /// </summary>
    [HttpGet("scan/{uniqueId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(BeehiveScanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ScanLookup(Guid uniqueId)
    {
        var result = await _service.GetScanInfoAsync(uniqueId);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Returns whether the current authenticated user has access to view the beehive.
    /// Used by the scan flow to decide between redirecting to the detail page or showing a "no access" screen.
    /// </summary>
    [HttpGet("{id:int}/has-access")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> HasAccess(int id)
    {
        var hasAccess = await _service.CanCurrentUserAccessAsync(id);
        return Ok(new { hasAccess });
    }

    /// <summary>
    /// Regenerates QR codes for all existing beehives to use the current scan URL format.
    /// SystemAdmin only — run once after deploying this update.
    /// </summary>
    [HttpPost("regenerate-qr-codes")]
    [Authorize(Roles = Roles.SystemAdmin)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> RegenerateQrCodes()
    {
        var count = await _service.RegenerateAllQrCodesAsync();
        return Ok(new { updated = count, message = $"QR codes regenerated for {count} beehive(s)." });
    }

    /// <summary>
    /// Resolves an on-device–recognised number to the caller's beehives (by LabelNumber, else a number
    /// parsed from the name). Cheap path — no image upload, no AI. Returns 0/1/many matches.
    /// </summary>
    [HttpPost("resolve-by-number")]
    [ProducesResponseType(typeof(BeehiveNumberMatchResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResolveByNumber([FromBody] ResolveByNumberDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Number) || dto.Number.Length > 20)
            return BadRequest(new { message = "Broj košnice je obavezan." });

        var result = await _service.MatchByNumberAsync(dto.Number);
        return Ok(result);
    }

    /// <summary>
    /// Fallback path: reads the number from a photo via the Groq vision model, then matches it to the
    /// caller's beehives. Paid AI call → rate limited. Images only (JPEG/PNG/WebP), max 8 MB.
    /// </summary>
    [HttpPost("scan-by-number")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("photo-analyze")]
    [RequestSizeLimit(9_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 9_000_000)]
    [ProducesResponseType(typeof(BeehiveNumberMatchResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ScanByNumber(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Fotografija je obavezna." });

        if (string.IsNullOrWhiteSpace(file.ContentType) ||
            !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Podržane su samo slike (JPEG, PNG, WebP)." });

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);

        var result = await _service.ScanByNumberAsync(buffer.ToArray(), file.ContentType);
        return Ok(result);
    }

    /// <summary>
    /// One-time backfill: fills empty LabelNumbers from a number parsed from each hive's name.
    /// SystemAdmin only — run once after deploying the "scan by number" feature.
    /// </summary>
    [HttpPost("backfill-label-numbers")]
    [Authorize(Roles = Roles.SystemAdmin)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> BackfillLabelNumbers()
    {
        var count = await _service.BackfillLabelNumbersFromNamesAsync();
        return Ok(new { updated = count, message = $"Label set for {count} beehive(s) from their names." });
    }

    /// <summary>Returns all beehives accessible to the current user (role-scoped). Used by global search.</summary>
    [HttpGet("all")]
    [ProducesResponseType(typeof(IEnumerable<BeehiveDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var beehives = await _service.GetAllForCurrentUserAsync();
        return Ok(beehives);
    }

    /// <summary>Returns the beehives in the apiary visible to the caller (Beekeepers see only assigned hives).</summary>
    [HttpGet("by-apiary/{apiaryId:int}")]
    [ProducesResponseType(typeof(IEnumerable<BeehiveDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByApiary(int apiaryId)
    {
        var beehives = await _service.GetByApiaryIdAsync(apiaryId);
        return Ok(beehives);
    }

    /// <summary>Returns the QR codes of the apiary's beehives, for label printing/export.</summary>
    [HttpGet("by-apiary/{apiaryId:int}/qr-codes")]
    [ProducesResponseType(typeof(IEnumerable<BeehiveQrDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQrCodesByApiary(int apiaryId)
    {
        var qrCodes = await _service.GetQrCodesByApiaryAsync(apiaryId);
        return Ok(qrCodes);
    }

    /// <summary>Returns a single beehive including all its inspections, scoped to the caller's access.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BeehiveDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var beehive = await _service.GetByIdAsync(id);
        return Ok(beehive);
    }

    /// <summary>Creates a new beehive within an apiary. ApiaryAdmin, OrgAdmin, and SystemAdmin only.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Managers)]
    [ProducesResponseType(typeof(BeehiveDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateBeehiveDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates an existing beehive. ApiaryAdmin, OrgAdmin, and SystemAdmin only.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Managers)]
    [ProducesResponseType(typeof(BeehiveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBeehiveDto dto)
    {
        var validation = await _updateValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var updated = await _service.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Deletes a beehive and all its inspections. ApiaryAdmin, OrgAdmin, and SystemAdmin only.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Managers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
