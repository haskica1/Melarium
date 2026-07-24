using Melarium.Domain.Common;

namespace Melarium.Domain.Entities;

/// <summary>The per-hive line of a <see cref="Treatment"/>.</summary>
public class TreatmentEntry : BaseEntity
{
    public int TreatmentId { get; set; }
    public Treatment Treatment { get; set; } = null!;

    public int BeehiveId { get; set; }
    public Beehive Beehive { get; set; } = null!;

    /// <summary>Per-hive dose note — only when it deviates from the treatment's <see cref="Treatment.DosePerHive"/>.</summary>
    public string? DoseNote { get; set; }
}
