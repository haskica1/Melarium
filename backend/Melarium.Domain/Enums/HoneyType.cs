namespace Melarium.Domain.Enums;

/// <summary>
/// The botanical type of an extracted honey. English member names follow the codebase
/// convention (see <see cref="QueenOrigin"/>); Bosnian display labels live in <c>BsLabels</c>.
/// </summary>
public enum HoneyType
{
    Acacia    = 1,  // Bagrem
    Linden    = 2,  // Lipa
    Chestnut  = 3,  // Kesten
    Sunflower = 4,  // Suncokret
    Meadow    = 5,  // Livadski
    Forest    = 6,  // Šumski
    Rapeseed  = 7,  // Uljana repica
    Other     = 99, // Ostalo
}
