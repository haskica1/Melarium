using Melarium.Application.Features.Invitations;
using Melarium.Application.Features.Invitations.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Melarium.API.Controllers;

/// <summary>
/// "Pozovi prijatelja" (SPEC-15) — Phase 1: the personal share link, the caller's own history, and
/// the anonymous lookup that feeds the banner on /register.
/// </summary>
/// <remarks>
/// No <c>IAccessGuard</c>: an invitation is not part of the Organization → Apiary → Beehive
/// hierarchy, so scoping is simply "your own rows", resolved from the caller's id inside the service.
/// No <c>IPlanGuard</c> either — gating the ability to refer people would be self-defeating.
/// </remarks>
[ApiController]
[Route("api/invites")]
[Produces("application/json")]
[Authorize]
public class InvitesController : ControllerBase
{
    private readonly IInvitationService _service;

    public InvitesController(IInvitationService service) => _service = service;

    /// <summary>Stats and the share URL for the caller, minting their referral code on first view.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(InvitationSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary() => Ok(await _service.GetSummaryAsync());

    /// <summary>The caller's own invitations, newest first.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(IEnumerable<InvitationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine() => Ok(await _service.GetMineAsync());

    /// <summary>
    /// Resolves a referral code to the inviter's first name for the sign-up banner. Anonymous by
    /// necessity — the visitor has no account yet — and rate-limited like the other token lookups.
    /// Returns a first name only: a referral code identifies an invitation, not an account, so this
    /// confirms nothing about any e-mail address.
    /// </summary>
    [HttpGet("ref/{code}")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-token")]
    [ProducesResponseType(typeof(ReferralInviterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInviter(string code)
    {
        var inviter = await _service.GetInviterByCodeAsync(code);
        return inviter is null ? NotFound() : Ok(inviter);
    }
}
