namespace Melarium.Domain.Enums;

/// <summary>Active substance of a veterinary product. Bosnian display labels live in <c>BsLabels</c>.</summary>
public enum ActiveSubstance
{
    Amitraz        = 1,
    Flumethrin     = 2,  // Flumetrin
    TauFluvalinate = 3,  // Tau-fluvalinat
    Coumaphos      = 4,  // Kumafos
    OxalicAcid     = 5,  // Oksalna kiselina
    FormicAcid     = 6,  // Mravlja kiselina
    Thymol         = 7,  // Timol
    Other          = 99, // Ostalo
}
