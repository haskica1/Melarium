using Melarium.Application.Features.Profile;
using Melarium.Application.Features.Profile.DTOs;
using FluentValidation;
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
    private readonly IValidator<UpdateProfileDto> _updateValidator;
    private readonly IValidator<DeleteAccountDto> _deleteValidator;

    public ProfileController(
        IProfileService service,
        IValidator<UpdateProfileDto> updateValidator,
        IValidator<DeleteAccountDto> deleteValidator)
    {
        _service = service;
        _updateValidator = updateValidator;
        _deleteValidator = deleteValidator;
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var validation = await _updateValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var result = await _service.UpdateProfileAsync(dto);
        return Ok(result);
    }

    /// <summary>
    /// Describes what deleting the current account would do, so the confirmation screen asks the
    /// right question instead of re-deriving the rule on the client.
    /// </summary>
    [HttpGet("deletion-preview")]
    [ProducesResponseType(typeof(AccountDeletionPreviewDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDeletionPreview()
    {
        var preview = await _service.GetDeletionPreviewAsync();
        return Ok(preview);
    }

    /// <summary>
    /// Permanently deletes the current account. When the caller is the last member and the
    /// administrator of their organization, the organization and all of its records go too.
    /// </summary>
    /// <remarks>
    /// The body carries the password, so this is a DELETE with a body — allowed, and the honest
    /// shape here: the alternative (a password in the query string) would put a credential in
    /// server logs and browser history.
    /// </remarks>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteMyAccount([FromBody] DeleteAccountDto dto)
    {
        var validation = await _deleteValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        await _service.DeleteMyAccountAsync(dto);
        return NoContent();
    }
}
