namespace Melarium.Application.Features.Auth.DTOs;

/// <summary>Redeems a password-reset token and sets a new password.</summary>
public record ResetPasswordDto(string Token, string NewPassword);
