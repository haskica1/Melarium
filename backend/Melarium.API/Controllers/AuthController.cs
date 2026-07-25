using Melarium.Application.Common.Interfaces;
using Melarium.Application.Features.Auth;
using Melarium.Application.Features.Auth.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Melarium.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<RegisterDto> _registerValidator;
    private readonly IValidator<ResetPasswordDto> _resetPasswordValidator;
    private readonly ICurrentUser _currentUser;

    public AuthController(
        IAuthService authService,
        IValidator<RegisterDto> registerValidator,
        IValidator<ResetPasswordDto> resetPasswordValidator,
        ICurrentUser currentUser)
    {
        _authService = authService;
        _registerValidator = registerValidator;
        _resetPasswordValidator = resetPasswordValidator;
        _currentUser = currentUser;
    }

    /// <summary>Authenticates a user and returns a JWT token. Rate-limited per client IP.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);
        return Ok(result);
    }

    /// <summary>
    /// Registers a new account and its organisation, then returns tokens (auto-login).
    /// The registrant becomes the Organization Admin. Rate-limited per client IP.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("register")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var validation = await _registerValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var result = await _authService.RegisterAsync(dto);
        return Ok(result);
    }

    /// <summary>
    /// Exchanges a valid refresh token for a new access token + a rotated refresh token.
    /// Returns 401 if the token is invalid, expired, or has already been used (reuse revokes the chain).
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-refresh")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto dto)
    {
        var result = await _authService.RefreshAsync(dto.RefreshToken);
        return Ok(result);
    }

    /// <summary>Revokes the given refresh token. Idempotent.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequestDto dto)
    {
        await _authService.LogoutAsync(dto.RefreshToken);
        return NoContent();
    }

    /// <summary>
    /// Emails a single-use password-reset link. Always returns 204, whether or not the address is
    /// registered — a different response would let anyone test which emails have accounts.
    /// Rate-limited per client IP.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        await _authService.ForgotPasswordAsync(dto.Email ?? string.Empty);
        return NoContent();
    }

    /// <summary>
    /// Redeems a reset token and sets a new password. Signs the account out of every device.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var validation = await _resetPasswordValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        await _authService.ResetPasswordAsync(dto);
        return NoContent();
    }

    /// <summary>Redeems an email-verification token. Safe to call twice with the same token.</summary>
    [HttpPost("verify-email")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto dto)
    {
        await _authService.VerifyEmailAsync(dto.Token ?? string.Empty);
        return NoContent();
    }

    /// <summary>
    /// Re-sends the verification email to the signed-in user. Authenticated (not anonymous by
    /// address) so it cannot be used to send mail to arbitrary people.
    /// </summary>
    [HttpPost("resend-verification")]
    [Authorize]
    [EnableRateLimiting("auth-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResendVerification()
    {
        if (_currentUser.UserId is not int userId)
            return Unauthorized();

        await _authService.ResendVerificationEmailAsync(userId);
        return NoContent();
    }
}
