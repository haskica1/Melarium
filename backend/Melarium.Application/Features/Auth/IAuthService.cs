using Melarium.Application.Features.Auth.DTOs;

namespace Melarium.Application.Features.Auth;

public interface IAuthService
{
    /// <summary>Validates credentials and issues an access token + a new refresh token.</summary>
    Task<LoginResponseDto> LoginAsync(LoginDto dto);

    /// <summary>
    /// Registers a new self-service account: creates a brand-new organisation and the registrant
    /// as its Organization Admin, then immediately issues tokens (auto-login). Throws if the email
    /// is already taken.
    /// </summary>
    Task<LoginResponseDto> RegisterAsync(RegisterDto dto);

    /// <summary>
    /// Rotates a valid refresh token: revokes it and issues a fresh access + refresh token pair.
    /// Reuse of an already-rotated token revokes the user's whole active token set.
    /// </summary>
    Task<LoginResponseDto> RefreshAsync(string refreshToken);

    /// <summary>Revokes the given refresh token (idempotent — no error if unknown/already revoked).</summary>
    Task LogoutAsync(string refreshToken);
}
