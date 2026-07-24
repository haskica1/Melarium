namespace Melarium.Application.Features.Auth.DTOs;

/// <summary>Carries a refresh token for the /refresh and /logout endpoints.</summary>
public record RefreshRequestDto(string RefreshToken);
