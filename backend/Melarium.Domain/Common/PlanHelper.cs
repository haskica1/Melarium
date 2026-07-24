using Melarium.Domain.Enums;

namespace Melarium.Domain.Common;

/// <summary>
/// The effective plan is computed, never stored (SPEC-09): any paid/Partner plan whose
/// <c>PlanValidUntil</c> has passed behaves as <see cref="PlanType.Free"/>. No background
/// job flips anything. Dates are compared, not instants — a plan expiring today is valid
/// through the end of that day.
/// </summary>
public static class PlanHelper
{
    public static PlanType Effective(PlanType plan, DateTime? validUntil, DateTime utcNow)
    {
        if (validUntil.HasValue && validUntil.Value.Date < utcNow.Date)
            return PlanType.Free;
        return plan;
    }
}
