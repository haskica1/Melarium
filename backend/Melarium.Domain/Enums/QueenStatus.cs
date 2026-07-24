namespace Melarium.Domain.Enums;

/// <summary>Lifecycle status of a queen within its beehive. At most one Active queen per hive.</summary>
public enum QueenStatus
{
    Active   = 1,
    Replaced = 2,
    Died     = 3,
    Missing  = 4
}
