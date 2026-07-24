namespace Melarium.Application.Features.Auth.DTOs;

/// <summary>
/// Self-service sign-up payload. The registrant becomes the Organization Admin of a
/// brand-new organisation created from <see cref="OrganizationName"/>.
/// </summary>
public record RegisterDto(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string OrganizationName,
    string? OrganizationDescription
);
