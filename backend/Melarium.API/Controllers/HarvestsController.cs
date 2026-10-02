using Melarium.Application.Features.Harvests;
using Melarium.Application.Features.Harvests.DTOs;
using Melarium.Domain.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers;

/// <summary>
/// Records harvests (prinosi) — honey extractions and, since SPEC-30, every other bee product — per hive,
/// per apiary or for the whole organization. Access is apiary-scoped and enforced in the service layer:
/// managers write within their scope; a Beekeeper has read-only access to harvests that contain at
/// least one of their assigned hives. Writing a product other than honey needs Standard or above.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class HarvestsController : ControllerBase
{
    private readonly IHarvestService _service;
    private readonly IValidator<CreateHarvestDto> _createValidator;
    private readonly IValidator<UpdateHarvestDto> _updateValidator;

    public HarvestsController(
        IHarvestService service,
        IValidator<CreateHarvestDto> createValidator,
        IValidator<UpdateHarvestDto> updateValidator)
    {
        _service         = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>
    /// Returns role-scoped harvests, optionally filtered by apiary, hive, and/or year. Honey only unless
    /// <paramref name="allProducts"/> or <paramref name="productType"/> says otherwise (SPEC-30) — a
    /// client older than that keeps getting exactly what it can draw.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<HarvestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? apiaryId, [FromQuery] int? beehiveId, [FromQuery] int? year,
        [FromQuery] bool allProducts = false, [FromQuery] HiveProductType? productType = null)
    {
        var harvests = await _service.GetAllAsync(
            apiaryId, beehiveId, year, allProducts ? HarvestKind.All : HarvestKind.Honey, productType);
        return Ok(harvests);
    }

    /// <summary>A hive's yield of every product per year, from its per-hive lines (SPEC-30).</summary>
    [HttpGet("hive/{beehiveId:int}/summary")]
    [ProducesResponseType(typeof(HiveHarvestSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHiveSummary(int beehiveId)
    {
        var summary = await _service.GetHiveSummaryAsync(beehiveId);
        return Ok(summary);
    }

    /// <summary>Season + per-year honey yield for a single hive (visible to anyone who can view the hive).</summary>
    [HttpGet("hive/{beehiveId:int}/yield")]
    [ProducesResponseType(typeof(HiveYieldDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHiveYield(int beehiveId)
    {
        var yield = await _service.GetHiveYieldAsync(beehiveId);
        return Ok(yield);
    }

    /// <summary>Returns a single harvest with its per-hive entries.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(HarvestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var harvest = await _service.GetByIdAsync(id);
        return Ok(harvest);
    }

    /// <summary>Creates a new harvest with per-hive entries.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(HarvestDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateHarvestDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates an existing harvest and replaces its entry set (apiary is immutable).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(HarvestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateHarvestDto dto)
    {
        var validation = await _updateValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var updated = await _service.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Deletes a harvest and its entries.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
