using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Treatments.DTOs;

/// <summary>List-view treatment with computed karenca/status.</summary>
public class TreatmentDto
{
    public int Id { get; set; }
    public int ApiaryId { get; set; }
    public string? ApiaryName { get; set; }

    public TreatmentPurpose Purpose { get; set; }
    public string PurposeName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public ActiveSubstance ActiveSubstance { get; set; }
    public string ActiveSubstanceName { get; set; } = string.Empty;
    public ApplicationMethod Method { get; set; }
    public string MethodName { get; set; } = string.Empty;
    public string DosePerHive { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int WithdrawalDays { get; set; }

    public int TotalRounds { get; set; }
    public int IntervalDays { get; set; }
    /// <summary>Completed rounds out of <see cref="TotalRounds"/> — drives the progress bar on the list.</summary>
    public int CompletedRounds { get; set; }

    public string? BatchNumber { get; set; }
    public string? Supplier { get; set; }
    public string? Notes { get; set; }

    /// <summary>End of the withdrawal window (computed).</summary>
    public DateTime KarencaUntil { get; set; }
    public TreatmentStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;

    public int HiveCount { get; set; }
    /// <summary>Hive names in this treatment (for the list chip and the PDF register).</summary>
    public List<string> HiveNames { get; set; } = [];
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}
