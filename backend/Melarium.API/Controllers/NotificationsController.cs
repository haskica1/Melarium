using System.Security.Claims;
using Melarium.Application.Common.Email;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Notifications;
using Melarium.Application.Features.Notifications.DTOs;
using Melarium.Infrastructure.Email;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _service;
    private readonly IEmailService _email;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<UpdateNotificationSettingsDto> _settingsValidator;
    private readonly IConfiguration _config;

    public NotificationsController(
        INotificationService service,
        IEmailService email,
        ICurrentUser currentUser,
        IValidator<UpdateNotificationSettingsDto> settingsValidator,
        IConfiguration config)
    {
        _service           = service;
        _email             = email;
        _currentUser       = currentUser;
        _settingsValidator = settingsValidator;
        _config            = config;
    }

    /// <summary>Returns all notifications for the current user, plus unread count.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (_currentUser.UserId is not int userId) return Unauthorized();

        var result = await _service.GetForUserAsync(userId);
        return Ok(result);
    }

    /// <summary>Marks all notifications for the current user as read.</summary>
    [HttpPatch("mark-all-read")]
    public async Task<IActionResult> MarkAllRead()
    {
        if (_currentUser.UserId is not int userId) return Unauthorized();

        await _service.MarkAllAsReadAsync(userId);
        return NoContent();
    }

    /// <summary>Marks a single notification as read.</summary>
    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        if (_currentUser.UserId is not int userId) return Unauthorized();

        await _service.MarkAsReadAsync(id, userId);
        return NoContent();
    }

    /// <summary>The caller's own notification preferences (SPEC-29); defaults until first saved.</summary>
    [HttpGet("settings")]
    [ProducesResponseType(typeof(NotificationSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings()
    {
        if (_currentUser.UserId is not int userId) return Unauthorized();

        return Ok(await _service.GetSettingsAsync(userId));
    }

    [HttpPut("settings")]
    [ProducesResponseType(typeof(NotificationSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateNotificationSettingsDto dto)
    {
        if (_currentUser.UserId is not int userId) return Unauthorized();

        var validation = await _settingsValidator.ValidateAsync(dto);
        if (!validation.IsValid) return BadRequest(validation.ToDictionary());

        return Ok(await _service.UpdateSettingsAsync(userId, dto));
    }

    /// <summary>Sends a test email to the current user's address. SystemAdmin only.</summary>
    [HttpPost("test-email")]
    [Authorize(Roles = Roles.SystemAdmin)]
    public async Task<IActionResult> TestEmail()
    {
        var emailClaim = User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)
                      ?? User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(emailClaim))
            return BadRequest("Could not determine email from token.");

        // suppressErrors: false — a diagnostic endpoint that reports success on a failed send
        // is worse than no endpoint. The rejection reason (unverified domain, bad API key) is
        // the whole point, and only a SystemAdmin can get here.
        // What was actually used to send — the point of a diagnostic mail.
        var content = new EmailContent("SMTP je ispravno podešen")
        {
            Subject = "SMTP test — radi ✅",
            Eyebrow = "Sistem",
            Icon = "✅",
            Tone = EmailTone.Success,
            Greet = false,
            Internal = true,
            Blocks =
            [
                new EmailText("Ako ste primili ovaj e-mail, slanje pošte sa Melarium servera radi kako treba."),
                new EmailFacts(
                [
                    new EmailFact("Server", $"{_config["Smtp:Host"]}:{_config["Smtp:Port"] ?? "587"}"),
                    new EmailFact("Pošiljalac", $"{_config["Smtp:FromName"] ?? "Melarium"} <{_config["Smtp:FromEmail"] ?? "noreply@melarium.app"}>"),
                    new EmailFact("Poslano", SentAt: true),
                ]),
            ],
            Reason = "Test poruka iz admin panela.",
        };
        var ctx = EmailContext.Create(_config, firstName: null, DateTime.UtcNow);

        try
        {
            await _email.SendAsync(
                emailClaim,
                "Test User",
                content.Subject,
                EmailTemplate.Render(content, ctx),
                suppressErrors: false,
                textBody: EmailTemplate.RenderText(content, ctx));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = $"SMTP send failed: {ex.Message}" });
        }

        return Ok(new { message = $"Test email sent to {emailClaim}." });
    }
}
