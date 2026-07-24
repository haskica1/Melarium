namespace Melarium.Application.Features.Auth.DTOs;

public record LoginResponseDto(
    string Token,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    int? OrganizationId,
    string? OrganizationName,
    IReadOnlyList<int> AssignedBeehiveIds
);
