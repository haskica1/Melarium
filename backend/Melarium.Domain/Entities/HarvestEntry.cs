using Melarium.Domain.Common;

namespace Melarium.Domain.Entities;

/// <summary>
/// The per-beehive line of a <see cref="Harvest"/>: how many kg were extracted from one hive.
/// </summary>
public class HarvestEntry : BaseEntity
{
    public int HarvestId { get; set; }
    public Harvest Harvest { get; set; } = null!;

    public int BeehiveId { get; set; }
    public Beehive Beehive { get; set; } = null!;

    public decimal QuantityKg { get; set; }

    /// <summary>Optional number of frames extracted from this hive.</summary>
    public int? FramesExtracted { get; set; }
}
