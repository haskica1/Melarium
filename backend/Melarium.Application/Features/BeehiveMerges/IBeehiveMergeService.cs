using Melarium.Application.Features.BeehiveMerges.DTOs;

namespace Melarium.Application.Features.BeehiveMerges;

/// <summary>Colony merge — sastavljanje društava (SPEC-19).</summary>
public interface IBeehiveMergeService
{
    /// <summary>
    /// Unites the source hive's colony into the target hive: the source leaves the apiary for good,
    /// its queen situation is resolved per <see cref="DTOs.CreateBeehiveMergeDto.QueenOutcome"/>, its
    /// open todos are deleted, it comes off its feeding programmes, and its in-progress treatment
    /// lines are annotated. One SaveChanges — see SPEC-19 §3.2.
    /// </summary>
    Task<BeehiveMergeDto> MergeAsync(CreateBeehiveMergeDto dto);

    /// <summary>Reverses a merge within the 24-hour window (SPEC-19 §4), restoring everything.</summary>
    Task<BeehiveMergeDto> UndoAsync(int mergeId);

    /// <summary>Merges this hive received, in force only, newest first.</summary>
    Task<IEnumerable<BeehiveMergeDto>> GetReceivedByBeehiveAsync(int beehiveId);

    /// <summary>What merging this hive away would do — numbers for the confirm dialog (§7.2).</summary>
    Task<MergePreviewDto> GetPreviewAsync(int sourceBeehiveId, int? targetBeehiveId);
}
