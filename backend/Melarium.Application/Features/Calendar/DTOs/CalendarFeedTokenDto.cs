namespace Melarium.Application.Features.Calendar.DTOs;

/// <summary>The user's current feed token + enabled flag. The controller composes the full URL from the request host.</summary>
public record CalendarFeedTokenDto(string Token, bool Enabled);
