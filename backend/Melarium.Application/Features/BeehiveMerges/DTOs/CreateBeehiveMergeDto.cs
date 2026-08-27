using Melarium.Domain.Enums;

namespace Melarium.Application.Features.BeehiveMerges.DTOs;

/// <summary>
/// Merge the colony of <see cref="SourceBeehiveId"/> into <see cref="TargetBeehiveId"/>.
/// The source hive is the one that leaves the apiary — practice unites the weaker colony into the
/// stronger one, which stays on its own stand (SPEC-19 §0).
/// </summary>
public class CreateBeehiveMergeDto
{
    public int SourceBeehiveId { get; set; }
    public int TargetBeehiveId { get; set; }
    public DateTime MergedAt { get; set; }

    public MergeReason Reason { get; set; }
    public MergeMethod Method { get; set; }
    public MergeQueenOutcome QueenOutcome { get; set; }

    public string? Notes { get; set; }
}
