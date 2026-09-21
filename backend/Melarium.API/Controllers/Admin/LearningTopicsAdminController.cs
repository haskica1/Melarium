using Melarium.Application.Common.Security;
using Melarium.Application.Features.Learning;
using Melarium.Application.Features.Learning.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Melarium.API.Controllers.Admin;

/// <summary>
/// Authoring of learning topics — SystemAdmin only. Includes unpublished drafts, the publish toggle
/// (first publish broadcasts one in-app notification per user) and the AI draft assist.
/// </summary>
[ApiController]
[Route("api/admin/learning-topics")]
[Produces("application/json")]
[Authorize(Roles = Roles.SystemAdmin)]
public class LearningTopicsAdminController : ControllerBase
{
    private readonly ILearningTopicService _service;
    private readonly IValidator<SaveLearningTopicDto> _saveValidator;
    private readonly IValidator<GenerateDraftDto> _draftValidator;
    private readonly IValidator<RejectLearningTopicDto> _rejectValidator;

    public LearningTopicsAdminController(
        ILearningTopicService service,
        IValidator<SaveLearningTopicDto> saveValidator,
        IValidator<GenerateDraftDto> draftValidator,
        IValidator<RejectLearningTopicDto> rejectValidator)
    {
        _service         = service;
        _saveValidator   = saveValidator;
        _draftValidator  = draftValidator;
        _rejectValidator = rejectValidator;
    }

    /// <summary>All topics, including unpublished drafts.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AdminLearningTopicDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var topics = await _service.GetAllForAdminAsync();
        return Ok(topics);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AdminLearningTopicDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var topic = await _service.GetByIdForAdminAsync(id);
        return Ok(topic);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AdminLearningTopicDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] SaveLearningTopicDto dto)
    {
        var validation = await _saveValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(AdminLearningTopicDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] SaveLearningTopicDto dto)
    {
        var validation = await _saveValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var updated = await _service.UpdateAsync(id, dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Publish toggle — publishing requires a non-empty body; only the first publish notifies users.</summary>
    [HttpPut("{id:int}/publish")]
    [ProducesResponseType(typeof(AdminLearningTopicDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPublished(int id, [FromBody] PublishLearningTopicDto dto)
    {
        var updated = await _service.SetPublishedAsync(id, dto.IsPublished);
        return Ok(updated);
    }

    // ── Review of user submissions (SPEC-26) ──────────────────────────────────

    /// <summary>Proposals waiting for review — the nav badge count.</summary>
    [HttpGet("submissions/summary")]
    [ProducesResponseType(typeof(LearningSubmissionSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubmissionSummary()
    {
        var summary = await _service.GetSubmissionSummaryAsync();
        return Ok(summary);
    }

    /// <summary>
    /// Approves a pending proposal and publishes it in one step — the author is notified, and the
    /// first publish broadcasts to everyone else exactly as an admin-authored topic does.
    /// </summary>
    [HttpPut("{id:int}/approve")]
    [ProducesResponseType(typeof(AdminLearningTopicDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Approve(int id)
    {
        var approved = await _service.ApproveAsync(id);
        return Ok(approved);
    }

    /// <summary>Rejects a pending proposal; the reason reaches the author verbatim.</summary>
    [HttpPut("{id:int}/reject")]
    [ProducesResponseType(typeof(AdminLearningTopicDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectLearningTopicDto dto)
    {
        var validation = await _rejectValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var rejected = await _service.RejectAsync(id, dto.Reason);
        return Ok(rejected);
    }

    /// <summary>AI draft assist — returns a markdown draft + summary for the form; never publishes.</summary>
    [HttpPost("generate-draft")]
    [EnableRateLimiting("ai-chat")]
    [ProducesResponseType(typeof(LearningDraftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateDraft([FromBody] GenerateDraftDto dto)
    {
        var validation = await _draftValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var draft = await _service.GenerateDraftAsync(dto);
        return Ok(draft);
    }
}
