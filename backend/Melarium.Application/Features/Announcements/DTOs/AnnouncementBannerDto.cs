namespace Melarium.Application.Features.Announcements.DTOs;

/// <summary>
/// What the layout needs on every page, in one call: the announcement to show in the banner
/// (null when there is none or the user has already seen the latest — D1) and the menu badge count.
/// The body travels with it so opening the modal costs no second request.
/// </summary>
public record AnnouncementBannerDto(AnnouncementDetailDto? Announcement, int UnreadCount);
