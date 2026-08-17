namespace Melarium.Application.Features.Admin.DTOs;

public class AdminOrganizationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int UserCount { get; set; }
    public int ApiaryCount { get; set; }
    public int BeehiveCount { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Newest sign of life in the organization, derived from the data itself rather than stored:
    /// the most recent create-or-update across every record the organization owns, plus token
    /// issue (sign-in / refresh). Null = nothing has ever happened beyond creating the row.
    /// See <c>IOrganizationRepository.GetLastActivityAsync</c> for the exact set.
    /// </summary>
    public DateTime? LastActivityAt { get; set; }

    // ── Subscription plan (SPEC-09) — admin list shows who pays ──
    public Domain.Enums.PlanType Plan { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public DateTime? PlanValidUntil { get; set; }
    public string? PlanNotes { get; set; }
}
