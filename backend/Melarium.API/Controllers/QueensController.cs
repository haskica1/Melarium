using Melarium.Application.Features.Queens;
using Melarium.Application.Features.Queens.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers;

/// <summary>
/// Tracks queens (matice) per beehive: the active queen plus the full replacement history.
/// Access follows the beehive rules (managers within scope, or a Beekeeper assigned to the hive)
/// and is enforced in the service layer.
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
[Authorize]
public class QueensController : ControllerBase
{
    private readonly IQueenService _service;
    private readonly IValidator<CreateQueenDto> _createValidator;
    private readonly IValidator<UpdateQueenDto> _updateValidator;

    public QueensController(
        IQueenService service,
        IValidator<CreateQueenDto> createValidator,
        IValidator<UpdateQueenDto> updateValidator)
    {
        _service         = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>Returns the queen history for a beehive, newest introduction first.</summary>
    [HttpGet("beehives/{beehiveId:int}/queens")]
    [ProducesResponseType(typeof(IEnumerable<QueenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByBeehive(int beehiveId)
    {
        var queens = await _service.GetByBeehiveIdAsync(beehiveId);
        return Ok(queens);
    }

    /// <summary>
    /// Registers a new active queen on the beehive. An existing active queen is automatically
    /// closed as Replaced in the same transaction.
    /// </summary>
    [HttpPost("beehives/{beehiveId:int}/queens")]
    [ProducesResponseType(typeof(QueenDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(int beehiveId, [FromBody] CreateQueenDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var created = await _service.CreateAsync(beehiveId, dto);
        return CreatedAtAction(nameof(GetByBeehive), new { beehiveId }, created);
    }

    /// <summary>Updates an existing queen record (including status changes).</summary>
    [HttpPut("queens/{id:int}")]
    [ProducesResponseType(typeof(QueenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateQueenDto dto)
    {
        var validation = await _updateValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var updated = await _service.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Returns the field-level edit history for a queen record, newest first.</summary>
    [HttpGet("queens/{id:int}/history")]
    [ProducesResponseType(typeof(IEnumerable<QueenEditLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(int id)
    {
        var history = await _service.GetEditHistoryAsync(id);
        return Ok(history);
    }

    /// <summary>Deletes a queen record (for correcting mistakes).</summary>
    [HttpDelete("queens/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
