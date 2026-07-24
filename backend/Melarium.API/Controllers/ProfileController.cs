using Melarium.Application.Features.Profile;
using Melarium.Application.Features.Profile.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _service;

    public ProfileController(IProfileService service)
    {
        _service = service;
    }

    /// <summary>Returns the current user's profile.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ProfileResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile()
    {
        var result = await _service.GetProfileAsync();
        return Ok(result);
    }

    /// <summary>Updates the current user's profile (name, email, optional password change).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ProfileResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var result = await _service.UpdateProfileAsync(dto);
        return Ok(result);
    }
}
