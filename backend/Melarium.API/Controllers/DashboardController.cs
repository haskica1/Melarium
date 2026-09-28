using Melarium.Application.Features.Dashboard;
using Melarium.Application.Features.Dashboard.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers;

/// <summary>
/// The start page (SPEC-29). Scope is the caller's own — the service reads it from the token and the
/// access guard — so there is no id anywhere. The org-less SystemAdmin gets 403.
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Produces("application/json")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;

    public DashboardController(IDashboardService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get() => Ok(await _service.GetAsync());

    /// <summary>Three-day forecast per apiary with the season's frost verdict — its own request, never blocking the page.</summary>
    [HttpGet("weather")]
    [ProducesResponseType(typeof(IReadOnlyList<ApiaryWeatherDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWeather() => Ok(await _service.GetWeatherAsync());
}
