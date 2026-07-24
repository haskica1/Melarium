using Melarium.Application.Features.Calendar;
using Melarium.Application.Features.Calendar.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class CalendarController : ControllerBase
{
    private readonly ICalendarService _service;
    private readonly ICalendarFeedService _feed;

    public CalendarController(ICalendarService service, ICalendarFeedService feed)
    {
        _service = service;
        _feed = feed;
    }

    /// <summary>Returns all calendar events (todos with due dates + diet feeding entries) for the current user.</summary>
    [HttpGet("events")]
    [ProducesResponseType(typeof(CalendarEventsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents()
    {
        var result = await _service.GetCalendarEventsAsync();
        return Ok(result);
    }

    /// <summary>The current user's private ICS feed URL — generates the secret token on first call (SPEC-11).</summary>
    [HttpGet("feed-url")]
    public async Task<IActionResult> GetFeedUrl()
    {
        var feed = await _feed.EnsureFeedTokenAsync();
        return Ok(new { url = BuildFeedUrl(feed.Token), enabled = feed.Enabled });
    }

    /// <summary>Issues a fresh feed token, invalidating the previously shared URL.</summary>
    [HttpPost("feed-url/rotate")]
    public async Task<IActionResult> RotateFeedUrl()
    {
        var feed = await _feed.RotateFeedTokenAsync();
        return Ok(new { url = BuildFeedUrl(feed.Token), enabled = feed.Enabled });
    }

    /// <summary>The current user's calendar sync preferences (category toggles + daily agenda).</summary>
    [HttpGet("settings")]
    [ProducesResponseType(typeof(CalendarSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings() => Ok(await _feed.GetSettingsAsync());

    [HttpPut("settings")]
    [ProducesResponseType(typeof(CalendarSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateCalendarSettingsDto dto) =>
        Ok(await _feed.UpdateSettingsAsync(dto));

    /// <summary>
    /// Anonymous, token-authenticated ICS subscription feed (RFC 5545). The token in the URL is the
    /// only credential — the "secret address" model. Unknown token or disabled feed → 404.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("feed/{token}.ics")]
    [Produces("text/calendar")]
    public async Task<IActionResult> Feed(string token)
    {
        var ics = await _feed.BuildFeedAsync(token);
        if (ics is null) return NotFound();
        return Content(ics, "text/calendar; charset=utf-8");
    }

    private string BuildFeedUrl(string token) =>
        $"{Request.Scheme}://{Request.Host}{Request.PathBase}/api/calendar/feed/{token}.ics";
}
