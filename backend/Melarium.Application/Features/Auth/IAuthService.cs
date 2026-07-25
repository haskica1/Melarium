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

    /// <summary>
    /// Emails a single-use reset link if an account with this address exists. Always completes
    /// silently — callers must not be able to tell registered addresses from unregistered ones.
    /// </summary>
    Task ForgotPasswordAsync(string email);

    /// <summary>
    /// Redeems a reset token and sets the new password, then signs the user out everywhere
    /// (whoever prompted the reset must not keep a live session). Throws when the token is
    /// unknown, already used, or expired.
    /// </summary>
    Task ResetPasswordAsync(ResetPasswordDto dto);

    /// <summary>Redeems an email-verification token. Throws when it is unknown, used, or expired.</summary>
    Task VerifyEmailAsync(string token);

    /// <summary>Re-sends the verification email to the given user. No-op if already verified.</summary>
    Task ResendVerificationEmailAsync(int userId);
}
