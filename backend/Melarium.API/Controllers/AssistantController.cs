using FluentValidation;
using Melarium.Application.Features.Assistant;
using Melarium.Application.Features.Assistant.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Melarium.API.Controllers;

/// <summary>
/// AI assistant (SPEC-17): a spoken or typed command becomes proposals the user confirms, and only
/// then are records created. Ownership is enforced in the service layer from the JWT identity, and a
/// session belonging to somebody else returns 404 rather than 403.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class AssistantController : ControllerBase
{
    private readonly IAiAssistantService _service;
    private readonly IValidator<StartAssistantSessionDto> _startValidator;
    private readonly IValidator<AssistantTurnRequestDto> _turnValidator;
    private readonly IValidator<ConfirmActionsDto> _confirmValidator;

    public AssistantController(
        IAiAssistantService service,
        IValidator<StartAssistantSessionDto> startValidator,
        IValidator<AssistantTurnRequestDto> turnValidator,
        IValidator<ConfirmActionsDto> confirmValidator)
    {
        _service          = service;
        _startValidator   = startValidator;
        _turnValidator    = turnValidator;
        _confirmValidator = confirmValidator;
    }

    /// <summary>The caller's assistant sessions, newest activity first.</summary>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(IEnumerable<AssistantSessionSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessions() => Ok(await _service.GetSessionsAsync());

    /// <summary>One session with its turns and proposals (404 if not owned by the caller).</summary>
    [HttpGet("sessions/{id:int}")]
    [ProducesResponseType(typeof(AssistantSessionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSession(int id) => Ok(await _service.GetSessionAsync(id));

    /// <summary>Interprets a command and returns the session with the proposals awaiting confirmation.</summary>
    [HttpPost("sessions")]
    [EnableRateLimiting("ai-chat")]
    [ProducesResponseType(typeof(AssistantSessionDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> StartSession([FromBody] StartAssistantSessionDto dto)
    {
        var validation = await _startValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var created = await _service.StartSessionAsync(dto);
        return CreatedAtAction(nameof(GetSession), new { id = created.Id }, created);
    }

    /// <summary>Continues a session with another command or an answer to the assistant's question.</summary>
    [HttpPost("sessions/{id:int}/turns")]
    [EnableRateLimiting("ai-chat")]
    [ProducesResponseType(typeof(AssistantSessionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> AddTurn(int id, [FromBody] AssistantTurnRequestDto dto)
    {
        var validation = await _turnValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        return Ok(await _service.AddTurnAsync(id, dto));
    }

    /// <summary>
    /// Executes the confirmed actions. The body is untrusted input like any form post — the service
    /// re-checks the target and re-runs the validators regardless of what was proposed.
    /// </summary>
    [HttpPost("turns/{id:int}/confirm")]
    [ProducesResponseType(typeof(AssistantConfirmResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Confirm(int id, [FromBody] ConfirmActionsDto dto)
    {
        var validation = await _confirmValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        return Ok(await _service.ConfirmAsync(id, dto));
    }

    /// <summary>Discards every pending proposal on a turn.</summary>
    [HttpPost("turns/{id:int}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(int id)
    {
        await _service.RejectAsync(id);
        return NoContent();
    }

    /// <summary>Transcribes a recorded command to text for review before it is sent.</summary>
    [HttpPost("transcribe")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("voice-parse")]
    [RequestSizeLimit(15_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 15_000_000)]
    [ProducesResponseType(typeof(AssistantTranscriptDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Transcribe(IFormFile? audio)
    {
        if (audio == null || audio.Length == 0)
            return BadRequest(new { message = "Audio zapis je obavezan." });

        await using var stream = audio.OpenReadStream();
        var transcript = await _service.TranscribeAsync(stream, audio.FileName);
        return Ok(new AssistantTranscriptDto(transcript));
    }

    /// <summary>Deletes a session and its history (owner only).</summary>
    [HttpDelete("sessions/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteSessionAsync(id);
        return NoContent();
    }
}
