using Melarium.Domain.Common;

namespace Melarium.Domain.Entities;

/// <summary>
/// The per-beehive line of a <see cref="Harvest"/>: how much of the product one hive gave.
/// </summary>
public class HarvestEntry : BaseEntity
{
    public int HarvestId { get; set; }
    public Harvest Harvest { get; set; } = null!;

    public int BeehiveId { get; set; }
    public Beehive Beehive { get; set; } = null!;

    /// <summary>Six decimals since SPEC-30 — grams of royal jelly, milligrams of venom.</summary>
    public decimal QuantityKg { get; set; }

    /// <summary>Optional number of frames extracted from this hive — honey only.</summary>
    public int? FramesExtracted { get; set; }
}
