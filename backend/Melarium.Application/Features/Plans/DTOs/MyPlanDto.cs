using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Plans.DTOs;

/// <summary>Current organization's plan + usage, for the /plans page and proactive UI gating.</summary>
public class MyPlanDto
{
    public PlanType Plan { get; set; }
    public string PlanName { get; set; } = string.Empty;

    /// <summary>Computed: an expired paid plan behaves as Free (PlanHelper).</summary>
    public PlanType EffectivePlan { get; set; }
    public string EffectivePlanName { get; set; } = string.Empty;

    public DateTime? PlanValidUntil { get; set; }

    /// <summary>"Probni period" marks the registration trial (frontend shows a trial note).</summary>
    public string? PlanNotes { get; set; }

    public PlanUsageDto Usage { get; set; } = new();
}

/// <summary>Usage meters; a null limit means unlimited for the effective plan.</summary>
public class PlanUsageDto
{
    public int Apiaries { get; set; }
    public int? ApiariesLimit { get; set; }

    public int Beehives { get; set; }
    public int? BeehivesLimit { get; set; }

    /// <summary>Additional accounts beyond the owner (ukupno naloga − 1).</summary>
    public int Members { get; set; }
    public int? MembersLimit { get; set; }

    public int AdvisorMessagesThisMonth { get; set; }
    /// <summary>0 = no advisor access on the effective plan; null = unlimited.</summary>
    public int? AdvisorMessagesLimit { get; set; }
}
