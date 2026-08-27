namespace Melarium.Application.Features.BeehiveMerges.DTOs;

/// <summary>
/// What merging this hive away would actually do, so the confirm dialog can state consequences with
/// real numbers instead of generic warnings (SPEC-19 §7.2). Read-only; nothing here writes.
/// </summary>
public class MergePreviewDto
{
    public int BeehiveId { get; set; }
    public string BeehiveName { get; set; } = string.Empty;
    public string ApiaryName { get; set; } = string.Empty;

    /// <summary>Open beehive-scoped todos — these get deleted (D3).</summary>
    public int OpenTodoCount { get; set; }

    /// <summary>Names of the active feeding programmes this hive would be taken off.</summary>
    public List<string> ActiveDietNames { get; set; } = [];

    /// <summary>Names of in-progress treatments whose entry gets the "stopped" note.</summary>
    public List<string> OngoingTreatmentNames { get; set; } = [];

    /// <summary>The hive's active queen, described for the radio labels. Null when queenless.</summary>
    public string? SourceQueenSummary { get; set; }

    /// <summary>The receiving hive's active queen. Null when it is queenless.</summary>
    public string? TargetQueenSummary { get; set; }

    /// <summary>
    /// Set when the hive is inside a withdrawal period: the bees carry it into the receiving hive.
    /// A warning, never a block (SPEC-19 §3.1).
    /// </summary>
    public DateTime? KarencaUntil { get; set; }
    public string? KarencaProductName { get; set; }
}
