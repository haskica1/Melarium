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

    /// <summary>
    /// True when this account is an additional member past the plan's <c>MaxMembers</c> and can only
    /// read (SPEC-24). The frontend uses it for a standing banner and to disable write controls; the
    /// server refuses the writes regardless.
    /// </summary>
    public bool IsReadOnlyMember { get; set; }

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

    // ── Downgrade lock (SPEC-24). How much of the org's own data its plan no longer reaches —
    // what the /plans page turns into "7 od 50 košnica je dostupno". Zero on a plan that fits. ──
    public int LockedApiaries { get; set; }
    public int LockedBeehives { get; set; }

    /// <summary>Additional members who lost write access because they rank past MembersLimit.</summary>
    public int ReadOnlyMembers { get; set; }

    /// <summary>AI assistant interactions this month — questions and commands both (SPEC-18).</summary>
    public int AiInteractionsThisMonth { get; set; }
    /// <summary>0 = no AI access on the effective plan; null = unlimited.</summary>
    public int? AiInteractionsLimit { get; set; }
}
