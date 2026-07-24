using Melarium.Application.Common.Security;
using Melarium.Application.Features.Pastures;
using Melarium.Application.Features.Pastures.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers;

/// <summary>
/// Apiary relocation history (selidbe, SPEC-10). Anyone who can view the apiary reads the history;
/// recording/correcting a move is an org-level decision (OrgAdmin/SystemAdmin, apiary-edit matrix).
/// </summary>
[ApiController]
[Route("api/apiaries/{apiaryId:int}/moves")]
[Produces("application/json")]
[Authorize]
public class ApiaryMovesController : ControllerBase
{
    private readonly IApiaryMoveService _service;
    private readonly IValidator<CreateApiaryMoveDto> _validator;
    private readonly IValidator<SetHomeLocationDto> _homeLocationValidator;

    public ApiaryMovesController(
        IApiaryMoveService service,
        IValidator<CreateApiaryMoveDto> validator,
        IValidator<SetHomeLocationDto> homeLocationValidator)
    {
        _service = service;
        _validator = validator;
        _homeLocationValidator = homeLocationValidator;
    }

    /// <summary>The apiary's move history, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ApiaryMoveDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll(int apiaryId)
    {
        var moves = await _service.GetByApiaryAsync(apiaryId);
        return Ok(moves);
    }

    /// <summary>Records a move; the apiary's current pasture and coordinates follow immediately.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.OrgManagers)]
    [ProducesResponseType(typeof(ApiaryMoveDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(int apiaryId, [FromBody] CreateApiaryMoveDto dto)
    {
        var validation = await _validator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var created = await _service.CreateAsync(apiaryId, dto);
        return CreatedAtAction(nameof(GetAll), new { apiaryId }, created);
    }

    /// <summary>Deletes the latest move (mistake correction) and reverts the apiary's pasture.</summary>
    [HttpDelete("{moveId:int}")]
    [Authorize(Roles = Roles.OrgManagers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int apiaryId, int moveId)
    {
        await _service.DeleteAsync(apiaryId, moveId);
        return NoContent();
    }

    /// <summary>Moves the apiary back to its matična lokacija using its captured Home coordinates.</summary>
    [HttpPost("return-home")]
    [Authorize(Roles = Roles.OrgManagers)]
    [ProducesResponseType(typeof(ApiaryMoveDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReturnHome(int apiaryId)
    {
        var created = await _service.ReturnHomeAsync(apiaryId);
        return CreatedAtAction(nameof(GetAll), new { apiaryId }, created);
    }

    /// <summary>Declares/corrects the apiary's matična lokacija without recording a move.</summary>
    [HttpPut("home-location")]
    [Authorize(Roles = Roles.OrgManagers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetHomeLocation(int apiaryId, [FromBody] SetHomeLocationDto dto)
    {
        var validation = await _homeLocationValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        await _service.SetHomeLocationAsync(apiaryId, dto.Latitude, dto.Longitude);
        return NoContent();
    }
}
