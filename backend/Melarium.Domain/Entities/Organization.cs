using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

public class Organization : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // ── Subscription plan (SPEC-09) ──
    public PlanType Plan { get; set; } = PlanType.Free;

    /// <summary>Plan expiry; null = bez isteka. The effective plan is computed via <see cref="PlanHelper"/>.</summary>
    public DateTime? PlanValidUntil { get; set; }

    /// <summary>Manual bookkeeping: broj uplatnice, ko je platio, "Probni period"…</summary>
    public string? PlanNotes { get; set; }

    public int? CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Apiary> Apiaries { get; set; } = new List<Apiary>();
}
