using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

/// <summary>
/// One veterinary treatment application (tretman), apiary-scoped, covering a set of that apiary's hives
/// via <see cref="Entries"/>. This is the legally-required medicine record (5-year retention).
/// </summary>
public class Treatment : BaseEntity
{
    public int ApiaryId { get; set; }
    public Apiary Apiary { get; set; } = null!;

    public TreatmentPurpose Purpose { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public ActiveSubstance ActiveSubstance { get; set; }
    public ApplicationMethod Method { get; set; }

    /// <summary>Dose per hive, free text (e.g. "2 trake po košnici", "5 ml po ulici pčela").</summary>
    public string DosePerHive { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    /// <summary>When strips were removed / application finished. Null = still in progress.</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Withdrawal period (karenca) in days; 0 = none.</summary>
    public int WithdrawalDays { get; set; }

    /// <summary>Number of applications, e.g. Apiguard = 2. 1 = single application (strips, trickling).</summary>
    public int TotalRounds { get; set; } = 1;

    /// <summary>Days between applications. Irrelevant when <see cref="TotalRounds"/> == 1.</summary>
    public int IntervalDays { get; set; }

    /// <summary>LOT / batch number — legally expected.</summary>
    public string? BatchNumber { get; set; }
    public string? Supplier { get; set; }
    public string? Notes { get; set; }

    public int? CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public List<TreatmentEntry> Entries { get; set; } = [];

    /// <summary>Scheduled application rounds — see <see cref="TotalRounds"/>/<see cref="IntervalDays"/>.</summary>
    public List<TreatmentRound> Rounds { get; set; } = [];
}
