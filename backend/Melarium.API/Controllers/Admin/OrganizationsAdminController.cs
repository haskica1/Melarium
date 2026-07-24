using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Admin;
using Melarium.Application.Features.Admin.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers.Admin;

/// <summary>
/// System administration of organizations — accessible only to SystemAdmin users.
/// </summary>
[ApiController]
[Route("api/admin/organizations")]
[Produces("application/json")]
[Authorize(Roles = Roles.SystemAdmin)]
public class OrganizationsAdminController : ControllerBase
{
    private readonly IAdminService _service;
    private readonly ICurrentUser _currentUser;

    public OrganizationsAdminController(IAdminService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AdminOrganizationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrganizations()
    {
        var orgs = await _service.GetAllOrganizationsAsync();
        return Ok(orgs);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AdminOrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrganization(int id)
    {
        var org = await _service.GetOrganizationByIdAsync(id);
        return Ok(org);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AdminOrganizationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrganization([FromBody] CreateOrganizationDto dto)
    {
        var created = await _service.CreateOrganizationAsync(dto, _currentUser.UserId);
        return CreatedAtAction(nameof(GetOrganization), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(AdminOrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateOrganization(int id, [FromBody] UpdateOrganizationDto dto)
    {
        var updated = await _service.UpdateOrganizationAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>
    /// Manual plan activation (SPEC-09 v1 billing): sets plan, expiry and bookkeeping note.
    /// Accepts all five plans including the hidden Partner plan.
    /// </summary>
    [HttpPut("{id:int}/plan")]
    [ProducesResponseType(typeof(AdminOrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateOrganizationPlan(
        int id,
        [FromBody] UpdateOrganizationPlanDto dto,
        [FromServices] FluentValidation.IValidator<UpdateOrganizationPlanDto> validator)
    {
        var validation = await validator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var updated = await _service.UpdateOrganizationPlanAsync(id, dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteOrganization(int id)
    {
        await _service.DeleteOrganizationAsync(id);
        return NoContent();
    }

    /// <summary>Returns apiaries for a given organization — used when assigning Admin users.</summary>
    [HttpGet("{id:int}/apiaries")]
    [ProducesResponseType(typeof(IEnumerable<AdminApiaryListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetApiariesByOrganization(int id)
    {
        var apiaries = await _service.GetApiariesByOrganizationAsync(id);
        return Ok(apiaries);
    }

    /// <summary>Returns all beehives for a given organization — used when assigning User role beekeepers.</summary>
    [HttpGet("{id:int}/beehives")]
    [ProducesResponseType(typeof(IEnumerable<AdminBeehiveListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBeehivesByOrganization(int id)
    {
        var beehives = await _service.GetBeehivesByOrganizationAsync(id);
        return Ok(beehives);
    }
}
