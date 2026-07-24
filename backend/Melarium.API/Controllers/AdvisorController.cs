using Melarium.Application.Features.Advisor;
using Melarium.Application.Features.Advisor.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Melarium.API.Controllers;

/// <summary>
/// AI beekeeping advisor (SPEC-01): personal chat conversations, optionally grounded in a hive's data.
/// Ownership is enforced in the service layer from the JWT identity.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class AdvisorController : ControllerBase
{
    private readonly IAdvisorService _service;
    private readonly IValidator<CreateConversationDto> _createValidator;
    private readonly IValidator<SendMessageDto> _sendValidator;

    public AdvisorController(
        IAdvisorService service,
        IValidator<CreateConversationDto> createValidator,
        IValidator<SendMessageDto> sendValidator)
    {
        _service         = service;
        _createValidator = createValidator;
        _sendValidator   = sendValidator;
    }

    /// <summary>The caller's conversations, newest activity first.</summary>
    [HttpGet("conversations")]
    [ProducesResponseType(typeof(IEnumerable<AdvisorConversationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConversations()
    {
        var conversations = await _service.GetConversationsAsync();
        return Ok(conversations);
    }

    /// <summary>A single conversation with its messages (404 if not owned by the caller).</summary>
    [HttpGet("conversations/{id:int}")]
    [ProducesResponseType(typeof(AdvisorConversationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConversation(int id)
    {
        var conversation = await _service.GetConversationAsync(id);
        return Ok(conversation);
    }

    /// <summary>Starts a new conversation and returns it with the first AI exchange.</summary>
    [HttpPost("conversations")]
    [EnableRateLimiting("ai-chat")]
    [ProducesResponseType(typeof(AdvisorConversationDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateConversation([FromBody] CreateConversationDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var created = await _service.CreateConversationAsync(dto);
        return CreatedAtAction(nameof(GetConversation), new { id = created.Id }, created);
    }

    /// <summary>Appends a user message and returns the user + assistant message pair.</summary>
    [HttpPost("conversations/{id:int}/messages")]
    [EnableRateLimiting("ai-chat")]
    [ProducesResponseType(typeof(AdvisorMessagePairDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SendMessage(int id, [FromBody] SendMessageDto dto)
    {
        var validation = await _sendValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var pair = await _service.SendMessageAsync(id, dto);
        return Ok(pair);
    }

    /// <summary>Transcribes a recorded voice note to text for review before sending.</summary>
    [HttpPost("transcribe")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("voice-parse")]
    [RequestSizeLimit(15_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 15_000_000)]
    [ProducesResponseType(typeof(TranscriptResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Transcribe(IFormFile? audio)
    {
        if (audio == null || audio.Length == 0)
            return BadRequest(new { message = "Audio zapis je obavezan." });

        await using var stream = audio.OpenReadStream();
        var transcript = await _service.TranscribeAsync(stream, audio.FileName);
        return Ok(new TranscriptResultDto(transcript));
    }

    /// <summary>Deletes a conversation (owner only).</summary>
    [HttpDelete("conversations/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteConversationAsync(id);
        return NoContent();
    }
}
