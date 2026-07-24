using Melarium.Application.Features.Calendar.DTOs;

namespace Melarium.Application.Features.Calendar;

/// <summary>
/// The private ICS calendar feed + per-user calendar settings (SPEC-11 Faza A). Authenticated methods
/// act on the current user; <see cref="BuildFeedAsync"/> is the anonymous, token-authenticated path.
/// </summary>
public interface ICalendarFeedService
{
    /// <summary>Returns the current feed token, generating one on first call.</summary>
    Task<CalendarFeedTokenDto> EnsureFeedTokenAsync();

    /// <summary>Issues a fresh token, invalidating the previous feed URL.</summary>
    Task<CalendarFeedTokenDto> RotateFeedTokenAsync();

    Task<CalendarSettingsDto> GetSettingsAsync();
    Task<CalendarSettingsDto> UpdateSettingsAsync(UpdateCalendarSettingsDto dto);

    /// <summary>Renders the ICS document for the feed identified by <paramref name="token"/>, or null if the token is unknown / the feed is disabled.</summary>
    Task<string?> BuildFeedAsync(string token);
}
