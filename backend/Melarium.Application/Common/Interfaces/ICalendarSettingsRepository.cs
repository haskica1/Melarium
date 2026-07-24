using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Per-user calendar settings / feed token data access (SPEC-11).</summary>
public interface ICalendarSettingsRepository : IRepository<CalendarSettings>
{
    /// <summary>The user's settings row, tracked for edits; null if never created.</summary>
    Task<CalendarSettings?> GetByUserIdAsync(int userId);

    /// <summary>Resolves the feed's owner from its secret token (read-only); null if unknown.</summary>
    Task<CalendarSettings?> GetByFeedTokenAsync(string token);
}
