namespace Melarium.Application.Features.Auth.DTOs;

/// <summary>Requests a password-reset link for the given address.</summary>
public record ForgotPasswordDto(string Email);
