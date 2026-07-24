using Melarium.Application.Common.Security;
using Melarium.Application.Features.Apiaries;
using Melarium.Application.Features.Apiaries.DTOs;
using Melarium.Application.Features.Weather;
using Melarium.Application.Features.Weather.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers;

/// <summary>
/// Manages apiary (pčelinjak) resources. Role-based ownership is enforced in the service layer;
/// controllers only perform input validation and coarse role gating.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class ApiariesController : ControllerBase
{
    private readonly IApiaryService _service;
    private readonly IWeatherService _weather;
    private readonly IValidator<CreateApiaryDto> _createValidator;
    private readonly IValidator<UpdateApiaryDto> _updateValidator;

    public ApiariesController(
        IApiaryService service,
        IWeatherService weather,
        IValidator<CreateApiaryDto> createValidator,
        IValidator<UpdateApiaryDto> updateValidator)
    {
        _service = service;
        _weather = weather;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>Returns the apiaries visible to the current caller, scoped to their role.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ApiaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var apiaries = await _service.GetAllForCurrentUserAsync();
        return Ok(apiaries);
    }

    /// <summary>Returns a single apiary including its beehives, scoped to the caller's access.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiaryDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var apiary = await _service.GetByIdAsync(id);
        return Ok(apiary);
    }

    /// <summary>Creates a new apiary in the caller's organization. OrgAdmin and SystemAdmin only.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.OrgManagers)]
    [ProducesResponseType(typeof(ApiaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateApiaryDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates an existing apiary. OrgAdmin and SystemAdmin only.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.OrgManagers)]
    [ProducesResponseType(typeof(ApiaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateApiaryDto dto)
    {
        var validation = await _updateValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var updated = await _service.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Deletes an apiary and all its child beehives/inspections. OrgAdmin and SystemAdmin only.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.OrgManagers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Returns a 7-day weather forecast for the apiary's location.</summary>
    [HttpGet("{id:int}/weather")]
    [ProducesResponseType(typeof(WeatherForecastDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWeather(int id)
    {
        var apiary = await _service.GetByIdAsync(id);

        if (!apiary.HasLocation)
            return BadRequest(new { message = "This apiary has no location set. Add latitude and longitude to enable weather forecasts." });

        var forecast = await _weather.GetForecastAsync(apiary.Latitude!.Value, apiary.Longitude!.Value);
        return Ok(forecast);
    }
}
