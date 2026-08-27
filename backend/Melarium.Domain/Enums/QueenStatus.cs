namespace Melarium.Domain.Enums;

/// <summary>Lifecycle status of a queen within its beehive. At most one Active queen per hive.</summary>
public enum QueenStatus
{
    Active   = 1,
    Replaced = 2,
    Died     = 3,
    Missing  = 4,

    /// <summary>
    /// Physically removed by the beekeeper — today only when a colony merge does not keep her
    /// (SPEC-19 D2). Neither "Died" nor "Replaced" is true when both queens of a merge are removed.
    /// </summary>
    Removed  = 5
}
