namespace Melarium.Application.Features.Profile.DTOs;

/// <param name="Phone">Null for accounts created before phone numbers existed.</param>
/// <param name="EmailVerifiedAt">Null when the address is unconfirmed — the UI then offers to re-send the link.</param>
public record ProfileResponseDto(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    DateTime? EmailVerifiedAt
);
