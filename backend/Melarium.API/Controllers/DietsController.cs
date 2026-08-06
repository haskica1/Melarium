using Melarium.Application.Features.Diets;
using Melarium.Application.Features.Diets.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers;

/// <summary>
/// Manages feeding programmes (diets). A programme belongs to an apiary and covers a chosen set of
/// its hives. Access is enforced in the service layer: managers write within scope, a Beekeeper reads
/// programmes containing one of their assigned hives and may tick a feeding round, but not create,
/// edit, delete or stop one.
/// </summary>
[ApiController]
[Route("api/feedings")]
[Produces("application/json")]
[Authorize]
public class DietsController : ControllerBase
{
    private readonly IDietService _service;
    private readonly IValidator<CreateDietDto>           _createValidator;
    private readonly IValidator<UpdateDietDto>           _updateValidator;
    private readonly IValidator<AddDietBeehivesDto>      _addBeehivesValidator;
    private readonly IValidator<CompleteEarlyDto>        _completeEarlyValidator;
    private readonly IValidator<CompleteFeedingEntryDto> _completeEntryValidator;

    public DietsController(
        IDietService service,
        IValidator<CreateDietDto> createValidator,
        IValidator<UpdateDietDto> updateValidator,
        IValidator<AddDietBeehivesDto> addBeehivesValidator,
        IValidator<CompleteEarlyDto> completeEarlyValidator,
        IValidator<CompleteFeedingEntryDto> completeEntryValidator)
    {
        _service                = service;
        _createValidator        = createValidator;
        _updateValidator        = updateValidator;
        _addBeehivesValidator   = addBeehivesValidator;
        _completeEarlyValidator = completeEarlyValidator;
        _completeEntryValidator = completeEntryValidator;
    }

    /// <summary>Lists feeding programmes the caller may see, optionally filtered by apiary, hive or year.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DietDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll([FromQuery] int? apiaryId, [FromQuery] int? beehiveId, [FromQuery] int? year)
    {
        var diets = await _service.GetAllAsync(apiaryId, beehiveId, year);
        return Ok(diets);
    }

    /// <summary>
    /// Active programmes for a whole apiary — one flat row per (hive × programme), so the hive badges
    /// on a list cost a single request. Declared before "{id:int}" so the literal route always wins.
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<DietActiveInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetActive([FromQuery] int apiaryId)
    {
        var active = await _service.GetActiveAsync(apiaryId);
        return Ok(active);
    }

    /// <summary>Returns a single programme with its rounds and its hives (including removed ones).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(DietDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var diet = await _service.GetByIdAsync(id);
        return Ok(diet);
    }

    /// <summary>Creates a programme for an apiary and auto-generates its feeding rounds.</summary>
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

    /// <summary>Updates a programme (only allowed when not completed/stopped). Never changes its hives.</summary>
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

    /// <summary>Deletes a programme (only allowed before it has started).</summary>
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

    /// <summary>Adds hives to a running programme. They join the schedule from now on.</summary>
    [HttpPost("{id:int}/beehives")]
    [ProducesResponseType(typeof(DietDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddBeehives(int id, [FromBody] AddDietBeehivesDto dto)
    {
        var validation = await _addBeehivesValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var updated = await _service.AddBeehivesAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Removes a hive from a programme. Soft — the link stays as history with its removal date.</summary>
    [HttpDelete("{id:int}/beehives/{beehiveId:int}")]
    [ProducesResponseType(typeof(DietDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveBeehive(int id, int beehiveId)
    {
        var updated = await _service.RemoveBeehiveAsync(id, beehiveId);
        return Ok(updated);
    }

    /// <summary>Stops a programme early.</summary>
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

    /// <summary>
    /// Marks one feeding round as done for the whole group, with an optional note. Returns the full
    /// detail because the tick also moves the programme's status and progress.
    /// The body is optional: ticking without a note is the normal case, and a client that predates
    /// the note field must keep working.
    /// </summary>
    [HttpPost("{dietId:int}/feeding-entries/{entryId:int}/complete")]
    [ProducesResponseType(typeof(DietDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CompleteFeedingEntry(int dietId, int entryId, [FromBody] CompleteFeedingEntryDto? dto = null)
    {
        dto ??= new CompleteFeedingEntryDto();

        var validation = await _completeEntryValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        await _service.CompleteFeedingEntryAsync(dietId, entryId, dto);
        var updated = await _service.GetByIdAsync(dietId);
        return Ok(updated);
    }
}
