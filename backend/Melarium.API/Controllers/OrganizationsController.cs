using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.OrgProfile;
using Melarium.Application.Features.OrgProfile.DTOs;
using Melarium.Application.Features.Plans.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers;

/// <summary>
/// Organization-scoped self-service endpoints: the subscription-plan summary (SPEC-09) and the
/// organization's own profile (SPEC-22). Everything here acts on the caller's organization, taken
/// from the JWT — administration of *any* organization lives under /api/admin/organizations.
/// </summary>
[ApiController]
[Route("api/organizations")]
[Produces("application/json")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly IPlanGuard _plan;
    private readonly ICurrentUser _currentUser;
    private readonly IOrgProfileService _profile;
    private readonly IValidator<UpdateMyOrganizationDto> _updateValidator;

    public OrganizationsController(
        IPlanGuard plan,
        ICurrentUser currentUser,
        IOrgProfileService profile,
        IValidator<UpdateMyOrganizationDto> updateValidator)
    {
        _plan = plan;
        _currentUser = currentUser;
        _profile = profile;
        _updateValidator = updateValidator;
    }

    /// <summary>Current organization's plan, limits and usage — any authenticated org member.</summary>
    [HttpGet("my-plan")]
    [ProducesResponseType(typeof(MyPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyPlan()
    {
        // The org-less SystemAdmin has no plan of their own.
        if (_currentUser.OrganizationId is not int organizationId)
            return NotFound();

        var plan = await _plan.GetMyPlanAsync(organizationId);
        return Ok(plan);
    }

    // ── Moja organizacija (SPEC-22) ───────────────────────────────────────────

    /// <summary>The caller's own organization. Readable by every member; only OrgAdmin may write.</summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(MyOrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyOrganization()
    {
        var org = await _profile.GetMyOrganizationAsync();
        return Ok(org);
    }

    /// <summary>Updates the caller's own organization (name, description). OrganizationAdmin only.</summary>
    [HttpPut("my")]
    [Authorize(Roles = Roles.OrganizationAdmin)]
    [ProducesResponseType(typeof(MyOrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMyOrganization([FromBody] UpdateMyOrganizationDto dto)
    {
        var validation = await _updateValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var updated = await _profile.UpdateMyOrganizationAsync(dto);
        return Ok(updated);
    }

    /// <summary>
    /// Uploads (or replaces) the organization logo. Max 2 MB; JPEG/PNG/WebP only, validated from
    /// the real header bytes. OrganizationAdmin only.
    /// </summary>
    [HttpPost("my/logo")]
    [Authorize(Roles = Roles.OrganizationAdmin)]
    [Consumes("multipart/form-data")]
    // 2 MB logo cap + multipart/form overhead.
    [RequestSizeLimit(2_500_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2_500_000)]
    [ProducesResponseType(typeof(MyOrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadLogo(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Logotip je obavezan." });

        await using var stream = file.OpenReadStream();
        var updated = await _profile.SetLogoAsync(stream, file.Length);
        return Ok(updated);
    }

    /// <summary>Streams the organization logo. Auth-checked — the storage bucket is never public.</summary>
    [HttpGet("my/logo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyLogo()
    {
        var (content, contentType) = await _profile.OpenLogoAsync();
        // Not cached for a day like an inspection photo: this URL never changes, so a replaced logo
        // would keep showing the old image until the cache expired.
        Response.Headers.CacheControl = "private, no-cache";
        return File(content, contentType);
    }

    /// <summary>Removes the organization logo (column + stored file). OrganizationAdmin only.</summary>
    [HttpDelete("my/logo")]
    [Authorize(Roles = Roles.OrganizationAdmin)]
    [ProducesResponseType(typeof(MyOrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMyLogo()
    {
        var updated = await _profile.RemoveLogoAsync();
        return Ok(updated);
    }
}
