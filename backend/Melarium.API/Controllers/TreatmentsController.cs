using Melarium.Application.Features.Treatments;
using Melarium.Application.Features.Treatments.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers;

/// <summary>
/// Records veterinary treatments (evidencija tretmana) per apiary, broken down by hive — the legally
/// required medicine register. Apiary-scoped access enforced in the service layer: managers write within
/// scope; a Beekeeper has read-only access to treatments that contain an assigned hive.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class TreatmentsController : ControllerBase
{
    private readonly ITreatmentService _service;
    private readonly IValidator<CreateTreatmentDto> _createValidator;
    private readonly IValidator<UpdateTreatmentDto> _updateValidator;
    private readonly IValidator<CompleteTreatmentRoundDto> _completeRoundValidator;

    public TreatmentsController(
        ITreatmentService service,
        IValidator<CreateTreatmentDto> createValidator,
        IValidator<UpdateTreatmentDto> updateValidator,
        IValidator<CompleteTreatmentRoundDto> completeRoundValidator)
    {
        _service                = service;
        _createValidator        = createValidator;
        _updateValidator        = updateValidator;
        _completeRoundValidator = completeRoundValidator;
    }

    /// <summary>Role-scoped treatments, optionally filtered by apiary, hive, and/or year.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TreatmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int? apiaryId, [FromQuery] int? beehiveId, [FromQuery] int? year)
    {
        var treatments = await _service.GetAllAsync(apiaryId, beehiveId, year);
        return Ok(treatments);
    }

    /// <summary>A single treatment with its per-hive entries.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TreatmentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var treatment = await _service.GetByIdAsync(id);
        return Ok(treatment);
    }

    /// <summary>Records a new treatment with per-hive entries.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TreatmentDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateTreatmentDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates a treatment and replaces its entry set (apiary is immutable).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(TreatmentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTreatmentDto dto)
    {
        var validation = await _updateValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var updated = await _service.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Deletes a treatment and its entries (mistake correction — legal retention is the user's responsibility).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Ticks one scheduled application round. Returns the full treatment because the tick also moves
    /// the round-progress count shown on the detail page. The body is optional: ticking without a note
    /// is the normal case.
    /// </summary>
    [HttpPost("{treatmentId:int}/rounds/{roundId:int}/complete")]
    [ProducesResponseType(typeof(TreatmentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CompleteRound(int treatmentId, int roundId, [FromBody] CompleteTreatmentRoundDto? dto = null)
    {
        dto ??= new CompleteTreatmentRoundDto();

        var validation = await _completeRoundValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        await _service.CompleteTreatmentRoundAsync(treatmentId, roundId, dto);
        var updated = await _service.GetByIdAsync(treatmentId);
        return Ok(updated);
    }
}
