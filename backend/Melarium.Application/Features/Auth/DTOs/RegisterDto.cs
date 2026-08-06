namespace Melarium.Application.Features.Auth.DTOs;

/// <summary>
/// Self-service sign-up payload. The registrant becomes the Organization Admin of a
/// brand-new organisation created from <see cref="OrganizationName"/>.
/// </summary>
public record RegisterDto(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Password,
    string OrganizationName,
    string? OrganizationDescription,

    /// <summary>
    /// Optional "pozovi prijatelja" code from <c>?ref=</c> (SPEC-15). A defaulted trailing parameter
    /// so every existing call site keeps compiling. An unknown, expired or malformed value is
    /// ignored — it must never fail a registration.
    /// </summary>
    string? ReferralCode = null
);
