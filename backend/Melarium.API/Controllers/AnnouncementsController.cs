using Melarium.Application.Features.Announcements;
using Melarium.Application.Features.Announcements.DTOs;
using Melarium.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers;

/// <summary>
/// Published announcements — "Šta je novo" (SPEC-21). Readable by every authenticated user, with no
/// plan or role targeting (D5). Authoring lives under <c>/api/admin/announcements</c>.
/// </summary>
[ApiController]
[Route("api/announcements")]
[Produces("application/json")]
[Authorize]
public class AnnouncementsController : ControllerBase
{
    private readonly IAnnouncementService _service;

    public AnnouncementsController(IAnnouncementService service)
    {
        _service = service;
    }

    /// <summary>Published announcements, newest first, optionally filtered by type.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(AnnouncementListDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] AnnouncementType? type)
    {
        var announcements = await _service.GetPublishedAsync(type);
        return Ok(announcements);
    }

    /// <summary>
    /// What the layout needs on every page: the banner announcement (null when there is none or the
    /// user already saw the latest) plus the unseen count for the menu badge. One call, not three.
    /// </summary>
    [HttpGet("banner")]
    [ProducesResponseType(typeof(AnnouncementBannerDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBanner()
    {
        var banner = await _service.GetBannerAsync();
        return Ok(banner);
    }

    /// <summary>A single published announcement with its markdown body.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AnnouncementDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var announcement = await _service.GetPublishedByIdAsync(id);
        return Ok(announcement);
    }

    /// <summary>Marks the announcement seen for the current user — idempotent.</summary>
    [HttpPost("{id:int}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(int id)
    {
        await _service.MarkReadAsync(id);
        return NoContent();
    }
}
