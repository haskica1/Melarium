using System.Security.Claims;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Notifications;
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

    public NotificationsController(INotificationService service, IEmailService email, ICurrentUser currentUser)
    {
        _service     = service;
        _email       = email;
        _currentUser = currentUser;
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

    /// <summary>Sends a test email to the current user's address. SystemAdmin only.</summary>
    [HttpPost("test-email")]
    [Authorize(Roles = Roles.SystemAdmin)]
    public async Task<IActionResult> TestEmail()
    {
        var emailClaim = User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)
                      ?? User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(emailClaim))
            return BadRequest("Could not determine email from token.");

        await _email.SendAsync(
            emailClaim,
            "Test User",
            "Melarium — SMTP Test",
            "<h2>✅ SMTP is working!</h2><p>If you received this, email delivery is configured correctly.</p>");

        return Ok(new { message = $"Test email sent to {emailClaim}." });
    }
}
