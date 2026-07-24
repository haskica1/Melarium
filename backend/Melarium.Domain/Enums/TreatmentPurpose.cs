namespace Melarium.Domain.Enums;

/// <summary>What a treatment targets. English member names per codebase convention; Bosnian labels in <c>BsLabels</c>.</summary>
public enum TreatmentPurpose
{
    Varroa     = 1,  // Varoa
    Nosema     = 2,  // Nozemoza
    Chalkbrood = 3,  // Krečno leglo
    Other      = 99, // Ostalo
}
