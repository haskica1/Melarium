using Melarium.Application.Features.Learning;
using Melarium.Application.Features.Learning.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Melarium.API.Controllers;

/// <summary>
/// Proposing a learning topic (SPEC-26) — any authenticated user writes a topic and sends it for
/// review; a SystemAdmin approves it through <c>/api/admin/learning-topics</c> before it appears in
/// Edukacija. Every route here is scoped to the caller's own proposals: someone else's id is a
/// <c>404</c>, never a <c>403</c>, so an id cannot be probed.
/// </summary>
[ApiController]
[Route("api/learning-topics/submissions")]
[Produces("application/json")]
[Authorize]
public class LearningSubmissionsController : ControllerBase
{
    private readonly ILearningTopicService _service;
    private readonly IValidator<SaveLearningTopicDto> _validator;

    public LearningSubmissionsController(ILearningTopicService service, IValidator<SaveLearningTopicDto> validator)
    {
        _service   = service;
        _validator = validator;
    }

    /// <summary>The caller's own proposals, newest first, in any review state.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MyLearningSubmissionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine()
    {
        var submissions = await _service.GetMySubmissionsAsync();
        return Ok(submissions);
    }

    /// <summary>One of the caller's own proposals, including the body and any rejection reason.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(MyLearningSubmissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMineById(int id)
    {
        var submission = await _service.GetMySubmissionAsync(id);
        return Ok(submission);
    }

    /// <summary>Sends a topic for review — saved unpublished and pending, never directly visible.</summary>
    [HttpPost]
    [EnableRateLimiting("learning-submit")]
    [ProducesResponseType(typeof(MyLearningSubmissionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit([FromBody] SaveLearningTopicDto dto)
    {
        var validation = await _validator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var created = await _service.SubmitAsync(dto);
        return CreatedAtAction(nameof(GetMineById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Edits the caller's own proposal. A rejected one goes back into the queue; an approved one is
    /// platform content and returns <c>422</c>.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(MyLearningSubmissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(int id, [FromBody] SaveLearningTopicDto dto)
    {
        var validation = await _validator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var updated = await _service.UpdateMySubmissionAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Withdraws a proposal that has not been approved.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Withdraw(int id)
    {
        await _service.WithdrawMySubmissionAsync(id);
        return NoContent();
    }
}
