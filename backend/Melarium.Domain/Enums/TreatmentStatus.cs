namespace Melarium.Domain.Enums;

/// <summary>
/// Derived (never stored) lifecycle of a treatment: application in progress, then the withdrawal
/// (karenca) window, then completed. Computed by <c>TreatmentStatusHelper</c>.
/// </summary>
public enum TreatmentStatus
{
    InProgress = 1,  // U toku — strips still in / application ongoing (EndDate not set)
    Karenca    = 2,  // Withdrawal period active
    Completed  = 3,  // Završen
}
