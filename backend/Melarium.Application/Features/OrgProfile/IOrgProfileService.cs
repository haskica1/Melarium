using Melarium.Application.Features.OrgProfile.DTOs;

namespace Melarium.Application.Features.OrgProfile;

/// <summary>
/// The caller's own organization (SPEC-22). Every method resolves the organization from the JWT
/// (<c>ICurrentUser.OrganizationId</c>) and never from a route parameter — that is what keeps a
/// tenant inside its own row without a per-call access check.
/// </summary>
public interface IOrgProfileService
{
    /// <summary>Reads the caller's organization. Any member of it may call this.</summary>
    Task<MyOrganizationDto> GetMyOrganizationAsync();

    /// <summary>Renames / re-describes the caller's organization. OrganizationAdmin only.</summary>
    Task<MyOrganizationDto> UpdateMyOrganizationAsync(UpdateMyOrganizationDto dto);

    /// <summary>Stores a new logo, replacing (and deleting) any previous one. OrganizationAdmin only.</summary>
    Task<MyOrganizationDto> SetLogoAsync(Stream content, long sizeBytes);

    /// <summary>Opens the stored logo for streaming. Any member of the organization may call this.</summary>
    Task<(Stream Content, string ContentType)> OpenLogoAsync();

    /// <summary>Removes the logo (column + stored file). OrganizationAdmin only.</summary>
    Task<MyOrganizationDto> RemoveLogoAsync();
}
