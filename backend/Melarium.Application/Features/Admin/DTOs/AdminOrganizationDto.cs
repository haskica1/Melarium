namespace Melarium.Application.Features.Admin.DTOs;

public class AdminOrganizationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int UserCount { get; set; }
    public int ApiaryCount { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }

    // ── Subscription plan (SPEC-09) — admin list shows who pays ──
    public Domain.Enums.PlanType Plan { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public DateTime? PlanValidUntil { get; set; }
    public string? PlanNotes { get; set; }
}
