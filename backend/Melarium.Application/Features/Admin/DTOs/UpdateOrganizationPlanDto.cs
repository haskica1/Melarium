using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Admin.DTOs;

/// <summary>Manual plan activation by SystemAdmin (SPEC-09 v1 billing). All five plans incl. Partner.</summary>
public record UpdateOrganizationPlanDto(PlanType Plan, DateTime? PlanValidUntil, string? PlanNotes);
