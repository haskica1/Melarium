namespace Melarium.Domain.Enums;

/// <summary>How a treatment is applied. Bosnian display labels live in <c>BsLabels</c>.</summary>
public enum ApplicationMethod
{
    Strips      = 1,  // Trake
    Trickling   = 2,  // Nakapavanje
    Sublimation = 3,  // Sublimacija
    Evaporation = 4,  // Isparavanje
    InFeed      = 5,  // U prihrani
    Spraying    = 6,  // Prskanje
    Other       = 99, // Ostalo
}
