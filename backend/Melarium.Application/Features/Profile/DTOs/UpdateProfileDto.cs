namespace Melarium.Application.Features.Profile.DTOs;

public record UpdateProfileDto(
    string FirstName,
    string LastName,
    string Email,
    string? CurrentPassword,
    string? NewPassword
);
