namespace Melarium.Application.Features.OrgProfile.DTOs;

/// <summary>Self-service organization edit (SPEC-22). OrganizationAdmin only — enforced on the controller.</summary>
/// <param name="SeasonShiftDays">
/// SPEC-29. Null leaves the stored shift alone, so a cached older client that knows nothing about the
/// field cannot reset an organization's season to the default by saving its name.
/// </param>
public record UpdateMyOrganizationDto(string Name, string? Description, int? SeasonShiftDays = null);
