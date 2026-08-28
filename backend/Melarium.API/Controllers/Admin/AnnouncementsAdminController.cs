using Melarium.Application.Common.Security;
using Melarium.Application.Features.Announcements;
using Melarium.Application.Features.Announcements.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers.Admin;

/// <summary>
/// Authoring of announcements — SystemAdmin only. Includes unpublished drafts and the publish
/// toggle. Publishing sends nothing: no e-mail, no bell item (SPEC-21 D4).
/// </summary>
[ApiController]
[Route("api/admin/announcements")]
[Produces("application/json")]
[Authorize(Roles = Roles.SystemAdmin)]
public class AnnouncementsAdminController : ControllerBase
{
    private readonly IAnnouncementService _service;
    private readonly IValidator<SaveAnnouncementDto> _saveValidator;

    public AnnouncementsAdminController(
        IAnnouncementService service,
        IValidator<SaveAnnouncementDto> saveValidator)
    {
        _service       = service;
        _saveValidator = saveValidator;
    }

    /// <summary>All announcements, including unpublished drafts.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AdminAnnouncementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var announcements = await _service.GetAllForAdminAsync();
        return Ok(announcements);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AdminAnnouncementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var announcement = await _service.GetByIdForAdminAsync(id);
        return Ok(announcement);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AdminAnnouncementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] SaveAnnouncementDto dto)
    {
        var validation = await _saveValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(AdminAnnouncementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] SaveAnnouncementDto dto)
    {
        var validation = await _saveValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var updated = await _service.UpdateAsync(id, dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Publish toggle — publishing requires a non-empty body; only the first publish stamps PublishedAt.</summary>
    [HttpPut("{id:int}/publish")]
    [ProducesResponseType(typeof(AdminAnnouncementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPublished(int id, [FromBody] PublishAnnouncementDto dto)
    {
        var updated = await _service.SetPublishedAsync(id, dto.IsPublished);
        return Ok(updated);
    }
}
