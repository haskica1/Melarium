namespace Melarium.Domain.Enums;

/// <summary>Why two colonies were merged into one (SPEC-19 §0).</summary>
public enum MergeReason
{
    /// <summary>Bezmatak — the colony lost its queen, or a virgin queen never returned from mating.</summary>
    Queenless     = 1,

    /// <summary>Lažne matice — laying workers; such a colony cannot be saved by adding a queen.</summary>
    LayingWorkers = 2,

    /// <summary>Too few bees or too little store to survive winter.</summary>
    WeakColony    = 3,

    /// <summary>Old or failing queen, poor brood pattern.</summary>
    PoorQueen     = 4,

    /// <summary>Deliberate consolidation — fewer, stronger colonies yield more honey.</summary>
    Consolidation = 5,

    /// <summary>Weak colonies were provoking robbing on the apiary.</summary>
    Robbing       = 6,

    Other         = 7,
}
