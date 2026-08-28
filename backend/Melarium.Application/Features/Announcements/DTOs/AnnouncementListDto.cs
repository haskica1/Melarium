namespace Melarium.Application.Features.Announcements.DTOs;

/// <summary>The "Šta je novo" page: every published announcement plus the unseen count.</summary>
public record AnnouncementListDto(IEnumerable<AnnouncementSummaryDto> Items, int UnreadCount);
