using FluentValidation;
using Melarium.Application.Features.Feedbacks;
using Melarium.Application.Features.Feedbacks.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Melarium.API.Controllers;

/// <summary>
/// Submitting feedback and following up on your own submissions (SPEC-13). Available to every
/// authenticated role — the triage side lives in <c>FeedbackAdminController</c>.
/// </summary>
[ApiController]
[Route("api/feedback")]
[Produces("application/json")]
[Authorize]
public class FeedbackController : ControllerBase
{
    private readonly IFeedbackService _service;
    private readonly IValidator<CreateFeedbackDto> _createValidator;

    public FeedbackController(IFeedbackService service, IValidator<CreateFeedbackDto> createValidator)
    {
        _service         = service;
        _createValidator = createValidator;
    }

    /// <summary>Submits feedback. Rate-limited: each accepted request lands in the operator's inbox.</summary>
    [HttpPost]
    [EnableRateLimiting("feedback")]
    [ProducesResponseType(typeof(FeedbackDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateFeedbackDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetMineById), new { id = created.Id }, created);
    }

    /// <summary>The caller's own submissions, newest first.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(IEnumerable<FeedbackDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine() => Ok(await _service.GetMineAsync());

    [HttpGet("mine/{id:int}")]
    [ProducesResponseType(typeof(FeedbackDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMineById(int id) => Ok(await _service.GetMineByIdAsync(id));

    /// <summary>
    /// Attaches one screenshot to your own submission. A separate request from the create call so the
    /// create endpoint stays plain JSON; the feedback is already recorded if this fails.
    /// </summary>
    [HttpPost("{id:int}/screenshot")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6_000_000)]
    [ProducesResponseType(typeof(FeedbackDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadScreenshot(int id, IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Slika je obavezna." });

        await using var stream = file.OpenReadStream();
        return Ok(await _service.AttachScreenshotAsync(id, stream, file.Length));
    }

    /// <summary>Screenshot bytes — the submitter or SystemAdmin only; the bucket is never public.</summary>
    [HttpGet("{id:int}/screenshot")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetScreenshot(int id)
    {
        var (content, contentType) = await _service.OpenScreenshotAsync(id);
        Response.Headers.CacheControl = "private, max-age=86400";
        return File(content, contentType);
    }
}
