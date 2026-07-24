using Melarium.Application.Features.Learning;
using Melarium.Application.Features.Learning.DTOs;
using Melarium.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers;

/// <summary>
/// Published learning topics (Edukacija) — platform-wide educational content readable by every
/// authenticated user. Authoring lives under <c>/api/admin/learning-topics</c>.
/// </summary>
[ApiController]
[Route("api/learning-topics")]
[Produces("application/json")]
[Authorize]
public class LearningTopicsController : ControllerBase
{
    private readonly ILearningTopicService _service;

    public LearningTopicsController(ILearningTopicService service)
    {
        _service = service;
    }

    /// <summary>Published topics, optionally filtered by category and/or month (1–12).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LearningTopicSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] LearningCategory? category, [FromQuery] int? month)
    {
        var topics = await _service.GetPublishedAsync(category, month);
        return Ok(topics);
    }

    /// <summary>A single published topic with its markdown body.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(LearningTopicDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var topic = await _service.GetPublishedByIdAsync(id);
        return Ok(topic);
    }

    /// <summary>Marks the topic read for the current user — idempotent.</summary>
    [HttpPost("{id:int}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(int id)
    {
        await _service.MarkReadAsync(id);
        return NoContent();
    }
}
