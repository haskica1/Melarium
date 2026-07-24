namespace Melarium.Domain.Enums;

/// <summary>How the queen arrived in the colony.</summary>
public enum QueenOrigin
{
    Purchased   = 1,
    OwnBreeding = 2,
    Swarm       = 3,
    /// <summary>Tiha zamjena — the colony superseded the old queen on its own.</summary>
    Supersedure = 4,
    Unknown     = 99
}
