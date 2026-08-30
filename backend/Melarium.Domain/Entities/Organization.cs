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

    // ── Logo (SPEC-22) ──

    /// <summary>
    /// Opaque <c>IFileStorage</c> key for the organization logo; null when none was uploaded.
    /// The blob is never public — it is streamed through the API like inspection photos (ADR-027).
    /// </summary>
    public string? LogoStoragePath { get; set; }

    /// <summary>Content type of the stored logo, sniffed from the file header bytes on upload.</summary>
    public string? LogoContentType { get; set; }

    public int? CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Apiary> Apiaries { get; set; } = new List<Apiary>();
}
