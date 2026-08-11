using Melarium.Application.Features.Plans.DTOs;

namespace Melarium.Application.Common.Security;

/// <summary>
/// Single source of truth for subscription-plan enforcement (SPEC-09, <see cref="IAccessGuard"/>
/// precedent). Limits come from config (<c>Plans:{PlanType}:{Key}</c>, absent = unlimited) and are
/// enforced <b>on create only</b> — downgrades never lock existing data. The effective plan is
/// computed (expired paid plan behaves as Free). The org-less SystemAdmin bypasses all gates.
/// Violations throw <see cref="Common.Exceptions.PlanLimitException"/> → HTTP 402.
/// </summary>
public interface IPlanGuard
{
    Task EnsureCanAddApiaryAsync(int organizationId);
    Task EnsureCanAddBeehiveAsync(int organizationId);

    /// <summary>Gate for creating additional member accounts (limit counts accounts beyond the owner).</summary>
    Task EnsureCanAddMemberAsync(int organizationId);

    /// <summary>Boolean feature gates: voice input, weekly summary, pastures, photo AI.</summary>
    Task EnsureFeatureAsync(int organizationId, PlanFeature feature);

    /// <summary>
    /// AI assistant gate (SPEC-17, merged with the advisor's own quota in SPEC-18): feature
    /// availability plus one per-organization monthly interaction quota, covering both questions and
    /// commands. Counted from the org's user turns in the current UTC calendar month — checked before
    /// the model is ever called, since a turn's kind is only known after that call returns.
    /// </summary>
    Task EnsureAiInteractionAsync(int organizationId);

    /// <summary>Effective plan + limits + usage for DTOs/UI.</summary>
    Task<MyPlanDto> GetMyPlanAsync(int organizationId);
}
