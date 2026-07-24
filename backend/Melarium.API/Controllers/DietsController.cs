using Melarium.Application.Features.Diets;
using Melarium.Application.Features.Diets.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers;

/// <summary>
/// Manages feeding programmes (diets) for beehives. Access to a beehive's diets is enforced in
/// the service layer (managers within scope, or a Beekeeper assigned to the hive).
/// </summary>
[ApiController]
[Route("api/feedings")]
[Produces("application/json")]
[Authorize]
public class DietsController : ControllerBase
{
    private readonly IDietService _service;
    private readonly IValidator<CreateDietDto>    _createValidator;
    private readonly IValidator<CopyDietDto>      _copyValidator;
    private readonly IValidator<UpdateDietDto>    _updateValidator;
    private readonly IValidator<CompleteEarlyDto> _completeEarlyValidator;

    public DietsController(
        IDietService service,
        IValidator<CreateDietDto> createValidator,
        IValidator<CopyDietDto> copyValidator,
        IValidator<UpdateDietDto> updateValidator,
        IValidator<CompleteEarlyDto> completeEarlyValidator)
    {
        _service                = service;
        _createValidator        = createValidator;
        _copyValidator          = copyValidator;
        _updateValidator        = updateValidator;
        _completeEarlyValidator = completeEarlyValidator;
    }

    /// <summary>Returns all diets for a given beehive, newest first.</summary>
    [HttpGet("by-beehive/{beehiveId:int}")]
    [ProducesResponseType(typeof(IEnumerable<DietDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByBeehive(int beehiveId)
    {
        var diets = await _service.GetByBeehiveIdAsync(beehiveId);
        return Ok(diets);
    }

    /// <summary>Returns a single diet with its feeding entries.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(DietDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var diet = await _service.GetByIdAsync(id);
        return Ok(diet);
    }

    /// <summary>Creates a new diet and auto-generates feeding entries.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(DietDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateDietDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Copies this diet's programme onto one or more other beehives the caller can access.</summary>
    [HttpPost("{id:int}/copy")]
    [ProducesResponseType(typeof(IEnumerable<DietDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Copy(int id, [FromBody] CopyDietDto dto)
    {
        var validation = await _copyValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var created = await _service.CopyToBeehivesAsync(id, dto);
        return Ok(created);
    }

    /// <summary>Updates a diet (only allowed when not completed/stopped).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(DietDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDietDto dto)
    {
        var validation = await _updateValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var updated = await _service.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Deletes a diet (only allowed before it has started).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Stops a diet early.</summary>
    [HttpPost("{id:int}/complete-early")]
    [ProducesResponseType(typeof(DietDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CompleteEarly(int id, [FromBody] CompleteEarlyDto dto)
    {
        var validation = await _completeEarlyValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var updated = await _service.CompleteEarlyAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Marks a specific feeding entry as completed.</summary>
    [HttpPost("{dietId:int}/feeding-entries/{entryId:int}/complete")]
    [ProducesResponseType(typeof(DietDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CompleteFeedingEntry(int dietId, int entryId)
    {
        await _service.CompleteFeedingEntryAsync(dietId, entryId);
        var updated = await _service.GetByIdAsync(dietId);
        return Ok(updated);
    }
}
