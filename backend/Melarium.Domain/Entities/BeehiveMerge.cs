using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

/// <summary>
/// One colony merge (sastavljanje društava, SPEC-19): the colony of <see cref="SourceBeehive"/> was
/// united into <see cref="TargetBeehive"/> on <see cref="MergedAt"/>. The source hive leaves the
/// apiary permanently (D1) but is never deleted — its inspections, harvests and, above all, its
/// legally-retained treatment entries stay readable.
///
/// The state ("is this hive still in the apiary") lives on <see cref="Beehive.MergedIntoBeehiveId"/>;
/// this row is the event. Same split as SPEC-10's <see cref="Apiary.CurrentPastureId"/> +
/// <see cref="ApiaryMove"/>. A receiving hive may collect several of these over the years.
/// </summary>
public class BeehiveMerge : BaseEntity
{
    /// <summary>Pripojena košnica — the one that leaves the apiary.</summary>
    public int SourceBeehiveId { get; set; }
    public Beehive SourceBeehive { get; set; } = null!;

    /// <summary>Prijemna košnica — the one that stays and receives the colony.</summary>
    public int TargetBeehiveId { get; set; }
    public Beehive TargetBeehive { get; set; } = null!;

    public DateTime MergedAt { get; set; }

    public MergeReason Reason { get; set; }
    public MergeMethod Method { get; set; }
    public MergeQueenOutcome QueenOutcome { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Everything the merge changed outside this table, captured so the 24-hour undo (D7) can put it
    /// back exactly — including the todos it deleted, which no other row remembers. Shape and
    /// restore order: SPEC-19 §4.
    /// </summary>
    public string? UndoJournalJson { get; set; }

    /// <summary>Null = in force. Set = undone; the row stays as the trace that it happened.</summary>
    public DateTime? UndoneAt { get; set; }

    public int? UndoneById { get; set; }
    public User? UndoneBy { get; set; }

    public int? CreatedById { get; set; }
    public User? CreatedBy { get; set; }
}
