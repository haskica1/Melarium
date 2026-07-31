namespace Melarium.Application.Features.Profile.DTOs;

/// <param name="Phone">Blank leaves the stored number unchanged — it never clears it.</param>
public record UpdateProfileDto(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? CurrentPassword,
    string? NewPassword
);
