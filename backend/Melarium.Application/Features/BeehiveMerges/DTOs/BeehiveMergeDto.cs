using Melarium.Domain.Enums;

namespace Melarium.Application.Features.BeehiveMerges.DTOs;

/// <summary>One colony merge as the client sees it (SPEC-19 §6).</summary>
public class BeehiveMergeDto
{
    public int Id { get; set; }

    public int SourceBeehiveId { get; set; }
    public string SourceBeehiveName { get; set; } = string.Empty;
    public int SourceApiaryId { get; set; }

    public int TargetBeehiveId { get; set; }
    public string TargetBeehiveName { get; set; } = string.Empty;
    public int TargetApiaryId { get; set; }

    public DateTime MergedAt { get; set; }

    public MergeReason Reason { get; set; }
    public string ReasonName { get; set; } = string.Empty;

    public MergeMethod Method { get; set; }
    public string MethodName { get; set; } = string.Empty;

    public MergeQueenOutcome QueenOutcome { get; set; }
    public string QueenOutcomeName { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>End of the 24-hour undo window. Null once it has passed or the merge was undone.</summary>
    public DateTime? CanUndoUntil { get; set; }

    public DateTime? UndoneAt { get; set; }
}
