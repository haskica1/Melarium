namespace Melarium.Domain.Enums;

/// <summary>
/// Subscription plan of an organization (SPEC-09). <see cref="Partner"/> is a hidden plan —
/// identical to <see cref="Max"/> in enforcement, assignable only by SystemAdmin and never
/// shown in public plan lists or (Phase 2) checkout.
/// </summary>
public enum PlanType
{
    Free     = 1,
    Standard = 2,
    Pro      = 3,
    Max      = 4,
    Partner  = 5,
}
