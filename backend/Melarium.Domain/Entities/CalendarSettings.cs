using Melarium.Domain.Common;

namespace Melarium.Domain.Entities;

/// <summary>
/// Per-user calendar-integration preferences (SPEC-11). Holds the secret ICS feed token and the
/// per-category sync toggles. Exactly one row per user, created lazily on first use.
/// </summary>
public class CalendarSettings : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>
    /// Opaque high-entropy token embedded in the private ICS feed URL. Stored as-is (not hashed) so
    /// the URL can be shown again to the user — the "secret address" model used by public iCal feeds.
    /// Null until the user first opens their feed. Rotating issues a fresh token, invalidating the old URL.
    /// </summary>
    public string? FeedToken { get; set; }

    public bool FeedEnabled { get; set; } = true;

    // Which obligation categories appear in the feed + daily agenda.
    public bool SyncFeedings { get; set; } = true;
    public bool SyncTodos { get; set; } = true;
    public bool SyncTreatments { get; set; } = true;
    public bool SyncInspections { get; set; } = true;

    /// <summary>When true, the user receives the daily 08:00 in-app + email agenda of that day's obligations.</summary>
    public bool DailyAgendaEnabled { get; set; } = true;
}
