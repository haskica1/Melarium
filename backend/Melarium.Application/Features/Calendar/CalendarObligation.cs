namespace Melarium.Application.Features.Calendar;

public enum ObligationKind
{
    Feeding        = 1,
    Todo           = 2,
    StripRemoval   = 3,
    KarencaEnd     = 4,
    InspectionDue  = 5,
    TreatmentRound = 6,
}

/// <summary>
/// A single dated beekeeping obligation, flattened from feedings, todos, and derived treatment /
/// inspection deadlines. One shared shape consumed by the ICS feed, the daily 08:00 agenda, and
/// (Faza B) native calendar sync. <see cref="IsSoft"/> marks a recomputed / moving deadline (e.g. a
/// recommended inspection) rather than a fixed appointment. <see cref="StableKey"/> is deterministic
/// per source item so it maps to a stable calendar UID.
/// </summary>
public sealed record CalendarObligation(
    ObligationKind Kind,
    string StableKey,
    DateOnly Date,
    string Title,
    string? Description,
    string? Location,
    int? BeehiveId,
    int? ApiaryId,
    bool IsSoft);
