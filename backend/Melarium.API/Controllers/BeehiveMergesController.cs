using FluentValidation;
using Melarium.Application.Features.BeehiveMerges;
using Melarium.Application.Features.BeehiveMerges.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers;

/// <summary>
/// Colony merges — sastavljanje društava (SPEC-19). Merging takes the source hive out of its apiary
/// permanently; only the 24-hour undo reverses it. Access (both apiaries, same organization) is
/// enforced in the service layer.
/// </summary>
[ApiController]
[Route("api/beehive-merges")]
[Produces("application/json")]
[Authorize]
public class BeehiveMergesController : ControllerBase
{
    private readonly IBeehiveMergeService _service;
    private readonly IValidator<CreateBeehiveMergeDto> _createValidator;

    public BeehiveMergesController(
        IBeehiveMergeService service,
        IValidator<CreateBeehiveMergeDto> createValidator)
    {
        _service         = service;
        _createValidator = createValidator;
    }

    /// <summary>Merges the source hive's colony into the target hive.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BeehiveMergeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateBeehiveMergeDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var created = await _service.MergeAsync(dto);
        return CreatedAtAction(nameof(GetReceivedByBeehive), new { beehiveId = created.TargetBeehiveId }, created);
    }

    /// <summary>Reverses a merge within 24 hours of it being recorded (SPEC-19 §4).</summary>
    [HttpPost("{id:int}/undo")]
    [ProducesResponseType(typeof(BeehiveMergeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Undo(int id)
    {
        var undone = await _service.UndoAsync(id);
        return Ok(undone);
    }

    /// <summary>Merges this hive received (it is the receiving hive), newest first.</summary>
    [HttpGet("by-beehive/{beehiveId:int}")]
    [ProducesResponseType(typeof(IEnumerable<BeehiveMergeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReceivedByBeehive(int beehiveId)
    {
        var merges = await _service.GetReceivedByBeehiveAsync(beehiveId);
        return Ok(merges);
    }

    /// <summary>
    /// What merging this hive away would do, so the confirm dialog can state real numbers.
    /// Pass the chosen receiving hive to also get its queen for the radio labels.
    /// </summary>
    [HttpGet("preview")]
    [ProducesResponseType(typeof(MergePreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPreview(
        [FromQuery] int sourceBeehiveId,
        [FromQuery] int? targetBeehiveId)
    {
        var preview = await _service.GetPreviewAsync(sourceBeehiveId, targetBeehiveId);
        return Ok(preview);
    }
}
