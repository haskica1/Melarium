using Melarium.Application.Common.Security;
using Melarium.Application.Features.Pastures;
using Melarium.Application.Features.Pastures.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers;

/// <summary>
/// Org-scoped pasture registry (pašnjaci) for migratory beekeeping (SPEC-10). Every role reads its
/// organization's registry; only OrgAdmin/SystemAdmin maintain it.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class PasturesController : ControllerBase
{
    private readonly IPastureService _service;
    private readonly IValidator<SavePastureDto> _validator;

    public PasturesController(IPastureService service, IValidator<SavePastureDto> validator)
    {
        _service = service;
        _validator = validator;
    }

    /// <summary>The caller's organization's pastures, with the count of apiaries currently on each.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PastureDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var pastures = await _service.GetAllAsync();
        return Ok(pastures);
    }

    [HttpPost]
    [Authorize(Roles = Roles.OrgManagers)]
    [ProducesResponseType(typeof(PastureDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] SavePastureDto dto)
    {
        var validation = await _validator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetAll), new { }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.OrgManagers)]
    [ProducesResponseType(typeof(PastureDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] SavePastureDto dto)
    {
        var validation = await _validator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var updated = await _service.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Deletes a pasture — 400 while any apiary sits on it or any move references it.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.OrgManagers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
