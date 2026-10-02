using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

/// <summary>
/// One collection of a bee product (prinos): an extraction of honey (vrcanje) — the only kind before
/// SPEC-30 — or wax, propolis, pollen, royal jelly, bee bread, venom or comb honey.
///
/// <para>
/// The quantity is recorded at exactly one level: split per hive (<see cref="Entries"/>), as one figure
/// for the apiary (<see cref="BulkKg"/> with an <see cref="ApiaryId"/>), or as one figure for the whole
/// organization (<see cref="BulkKg"/> with no apiary).
/// </para>
/// </summary>
public class Harvest : BaseEntity
{
    /// <summary>
    /// Carried on the row itself since SPEC-30, because a record for the whole organization has no
    /// apiary to reach its organization through. Backfilled from the apiary for older rows.
    /// </summary>
    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    /// <summary>
    /// Null = shared (zajednički): collected by the whole organization, e.g. wax rendered from every
    /// apiary's combs. The same meaning a null <c>Expense.ApiaryId</c> has — never "unknown".
    /// </summary>
    public int? ApiaryId { get; set; }
    public Apiary? Apiary { get; set; }

    public DateTime Date { get; set; }

    /// <summary>What was collected. Every harvest recorded before SPEC-30 is honey.</summary>
    public HiveProductType ProductType { get; set; } = HiveProductType.Honey;

    /// <summary>Botanical type — required for honey, always null for every other product.</summary>
    public HoneyType? HoneyType { get; set; }

    /// <summary>Optional sale price in KM per kg — per kg even for products the UI prices per gram.</summary>
    public decimal? PricePerKg { get; set; }

    /// <summary>
    /// The quantity when it is not split per hive — for the apiary, or for the whole organization when
    /// there is no apiary. Null when the quantity lives in <see cref="Entries"/>.
    /// </summary>
    public decimal? BulkKg { get; set; }

    public string? Notes { get; set; }

    public int? CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public List<HarvestEntry> Entries { get; set; } = [];
}
