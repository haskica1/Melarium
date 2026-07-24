using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Plans.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers;

/// <summary>
/// Organization-scoped self-service endpoints. Currently only the subscription-plan
/// summary (SPEC-09) — administration of organizations lives under /api/admin/organizations.
/// </summary>
[ApiController]
[Route("api/organizations")]
[Produces("application/json")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly IPlanGuard _plan;
    private readonly ICurrentUser _currentUser;

    public OrganizationsController(IPlanGuard plan, ICurrentUser currentUser)
    {
        _plan = plan;
        _currentUser = currentUser;
    }

    /// <summary>Current organization's plan, limits and usage — any authenticated org member.</summary>
    [HttpGet("my-plan")]
    [ProducesResponseType(typeof(MyPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyPlan()
    {
        // The org-less SystemAdmin has no plan of their own.
        if (_currentUser.OrganizationId is not int organizationId)
            return NotFound();

        var plan = await _plan.GetMyPlanAsync(organizationId);
        return Ok(plan);
    }
}
