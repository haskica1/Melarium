namespace Melarium.Domain.Enums;

/// <summary>
/// Which queen survives the merge (SPEC-19 D2). Practice removes the weaker colony's queen — but not
/// always: when the receiving colony is queenless, the surviving queen is the one that comes with the
/// merged-in colony. That is why this is chosen, never assumed.
/// </summary>
public enum MergeQueenOutcome
{
    /// <summary>The receiving hive keeps its queen; the source hive's queen is removed.</summary>
    KeptTarget = 1,

    /// <summary>The source hive's queen survives and moves to the receiving hive.</summary>
    KeptSource = 2,

    /// <summary>Neither — both are removed and the united colony stays queenless for now.</summary>
    None       = 3,
}
