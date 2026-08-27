using Melarium.Domain.Entities;

namespace Melarium.Application.Features.BeehiveMerges;

/// <summary>
/// The 24-hour undo window (SPEC-19 D7), in one place so the service that enforces it and the DTOs
/// that advertise it can never disagree.
///
/// <para>Measured from <see cref="Melarium.Domain.Common.BaseEntity.CreatedAt"/> — when the merge was
/// <i>recorded</i> — not from <c>MergedAt</c>, which is a date the user may backdate to when the
/// colonies were actually united.</para>
/// </summary>
public static class MergeUndoPolicy
{
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    /// <summary>Deadline of an open undo window, or null when it closed or the merge is already undone.</summary>
    public static DateTime? DeadlineFor(BeehiveMerge merge, DateTime utcNow)
    {
        if (merge.UndoneAt is not null) return null;
        var deadline = merge.CreatedAt.Add(Window);
        return deadline > utcNow ? deadline : null;
    }
}
