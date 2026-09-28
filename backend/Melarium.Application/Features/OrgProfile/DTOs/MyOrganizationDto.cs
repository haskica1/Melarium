using Melarium.Application.Common.Seasons;

namespace Melarium.Application.Features.OrgProfile.DTOs;

/// <summary>
/// The caller's own organization, as shown on "Moja organizacija" (SPEC-22). Deliberately not the
/// same shape as <c>AdminOrganizationDto</c>: plan bookkeeping and platform-wide fields belong to
/// the SystemAdmin screens, not to a tenant looking at itself.
/// </summary>
public class MyOrganizationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>The logo is streamed from a separate endpoint — the blob is never inlined here.</summary>
    public bool HasLogo { get; set; }

    public DateTime CreatedAt { get; set; }

    // Read-only context, so the page shows what the organization *is* and not just two inputs.
    public int UserCount { get; set; }
    public int ApiaryCount { get; set; }
    public int BeehiveCount { get; set; }

    // ── Season (SPEC-29) ──
    public int SeasonShiftDays { get; set; }

    /// <summary>This year's five phases under the stored shift, so the page shows what the number means.</summary>
    public IReadOnlyList<SeasonPhaseRange> SeasonPhases { get; set; } = [];
}
