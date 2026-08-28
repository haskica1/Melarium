using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Announcement ("Šta je novo") data access.</summary>
public interface IAnnouncementRepository : IRepository<Announcement>
{
    /// <summary>Published announcements, optionally filtered by type; newest first.</summary>
    Task<IEnumerable<Announcement>> GetPublishedAsync(AnnouncementType? type = null);

    /// <summary>The single newest published announcement — what the banner shows (D1), or null.</summary>
    Task<Announcement?> GetLatestPublishedAsync();

    /// <summary>A single published announcement, or null when missing/unpublished.</summary>
    Task<Announcement?> GetPublishedByIdAsync(int id);

    /// <summary>All announcements including drafts — admin listing, newest first.</summary>
    Task<IEnumerable<Announcement>> GetAllForAdminAsync();

    /// <summary>Announcement ids the user has seen — one query for the whole list (no N+1).</summary>
    Task<HashSet<int>> GetReadIdsAsync(int userId);

    /// <summary>How many published announcements the user has not seen — the menu badge (D8).</summary>
    Task<int> GetUnreadCountAsync(int userId);

    /// <summary>Whether the user already saw this announcement (idempotence guard).</summary>
    Task<bool> HasReadAsync(int announcementId, int userId);

    /// <summary>Stages a seen marker; persisted by the caller's SaveChanges.</summary>
    Task AddReadAsync(AnnouncementRead read);
}
