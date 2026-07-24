using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

/// <summary>
/// A single extraction event (vrcanje) for an apiary: how much honey of which type was extracted
/// on a given date, broken down per beehive via <see cref="Entries"/>.
/// </summary>
public class Harvest : BaseEntity
{
    public int ApiaryId { get; set; }
    public Apiary Apiary { get; set; } = null!;

    public DateTime Date { get; set; }

    public HoneyType HoneyType { get; set; }

    /// <summary>Optional sale price per kg (KM/kg), used for the revenue estimate on the stats page.</summary>
    public decimal? PricePerKg { get; set; }

    public string? Notes { get; set; }

    public int? CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public List<HarvestEntry> Entries { get; set; } = [];
}
