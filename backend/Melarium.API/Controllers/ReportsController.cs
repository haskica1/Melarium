using FluentValidation;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Reports;
using Melarium.Application.Features.Reports.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers;

/// <summary>
/// Consolidated beekeeping reports for an arbitrary period (SPEC-25) — the data behind the PDF and
/// Excel exports, which are rendered client-side from this one response.
///
/// <para>
/// Managers only: a Beekeeper is already read-only on harvests and treatments, and a report carries
/// the organization's finances. A caller without an organization gets 403 from the service.
/// </para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Roles = Roles.Managers)]
public class ReportsController : ControllerBase
{
    private readonly IReportService _service;
    private readonly IValidator<SeasonReportQueryDto> _validator;

    public ReportsController(IReportService service, IValidator<SeasonReportQueryDto> validator)
    {
        _service   = service;
        _validator = validator;
    }

    /// <summary>Yield, expenses, balance and treatment summary for the requested period.</summary>
    [HttpGet("season")]
    [ProducesResponseType(typeof(SeasonReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSeasonReport([FromQuery] SeasonReportQueryDto query)
    {
        var validation = await _validator.ValidateAsync(query);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        return Ok(await _service.GetSeasonReportAsync(query));
    }
}
