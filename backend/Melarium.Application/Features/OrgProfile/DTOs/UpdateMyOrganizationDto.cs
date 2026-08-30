namespace Melarium.Application.Features.OrgProfile.DTOs;

/// <summary>Self-service organization edit (SPEC-22). OrganizationAdmin only — enforced on the controller.</summary>
public record UpdateMyOrganizationDto(string Name, string? Description);
