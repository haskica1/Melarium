using FluentValidation;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Feedbacks;
using Melarium.Application.Features.Feedbacks.DTOs;
using Melarium.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Melarium.API.Controllers.Admin;

/// <summary>
/// Triage of user feedback (SPEC-13) — SystemAdmin only. Feedback is a platform-level channel between
/// a user and the operator, so it is gated by role here rather than by tenant scope.
/// </summary>
[ApiController]
[Route("api/admin/feedback")]
[Produces("application/json")]
[Authorize(Roles = Roles.SystemAdmin)]
public class FeedbackAdminController : ControllerBase
{
    private readonly IFeedbackService _service;
    private readonly IValidator<UpdateFeedbackStatusDto> _statusValidator;

    public FeedbackAdminController(
        IFeedbackService service, IValidator<UpdateFeedbackStatusDto> statusValidator)
    {
        _service         = service;
        _statusValidator = statusValidator;
    }

    /// <summary>Everything submitted, newest first. Both filters are optional.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AdminFeedbackDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] FeedbackType? type, [FromQuery] FeedbackStatus? status) =>
        Ok(await _service.GetAllForAdminAsync(type, status));

    /// <summary>Count of untriaged submissions — drives the nav badge.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(FeedbackSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary() => Ok(await _service.GetSummaryAsync());

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AdminFeedbackDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id) => Ok(await _service.GetByIdForAdminAsync(id));

    /// <summary>Moves the status and optionally leaves a reply; notifies the submitter when either changed.</summary>
    [HttpPut("{id:int}/status")]
    [ProducesResponseType(typeof(AdminFeedbackDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateFeedbackStatusDto dto)
    {
        var validation = await _statusValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        return Ok(await _service.UpdateStatusAsync(id, dto));
    }

    /// <summary>Removes a submission outright — spam/test cleanup. This is not a legal register.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
