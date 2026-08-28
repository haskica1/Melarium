using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Localization;
using Melarium.Application.Features.Announcements.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Announcements;

/// <summary>
/// Announcements — "Šta je novo" (SPEC-21). Platform-wide product news. Consumption endpoints only
/// ever see published announcements; authoring is SystemAdmin-only (role guard on the admin
/// controller). Publishing deliberately writes nothing to <c>Notification</c> (D4): the banner is
/// the notification, and a bell item would survive the banner being dismissed.
/// </summary>
public class AnnouncementService : IAnnouncementService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public AnnouncementService(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    // ── Consumption ──────────────────────────────────────────────────────────────

    public async Task<AnnouncementListDto> GetPublishedAsync(AnnouncementType? type)
    {
        var announcements = (await _uow.Announcements.GetPublishedAsync(type)).ToList();
        var readIds = await ReadIdsForCurrentUserAsync();

        return new AnnouncementListDto(
            announcements.Select(a => ToSummaryDto(a, readIds)),
            await UnreadCountForCurrentUserAsync());
    }

    /// <summary>
    /// The banner shows the newest published announcement and nothing else (D1). It is never a
    /// queue: an older announcement the user never saw does not resurface here once a newer one
    /// exists — the unread count and the "Šta je novo" page are what catch it.
    /// </summary>
    public async Task<AnnouncementBannerDto> GetBannerAsync()
    {
        var unreadCount = await UnreadCountForCurrentUserAsync();
        var latest = await _uow.Announcements.GetLatestPublishedAsync();

        if (latest is null || _currentUser.UserId is not int userId)
            return new AnnouncementBannerDto(null, unreadCount);

        if (await _uow.Announcements.HasReadAsync(latest.Id, userId))
            return new AnnouncementBannerDto(null, unreadCount);

        return new AnnouncementBannerDto(ToDetailDto(latest, isRead: false), unreadCount);
    }

    public async Task<AnnouncementDetailDto> GetPublishedByIdAsync(int id)
    {
        var announcement = await _uow.Announcements.GetPublishedByIdAsync(id)
            ?? throw new NotFoundException(nameof(Announcement), id);

        var readIds = await ReadIdsForCurrentUserAsync();
        return ToDetailDto(announcement, readIds.Contains(announcement.Id));
    }

    public async Task MarkReadAsync(int id)
    {
        var announcement = await _uow.Announcements.GetPublishedByIdAsync(id)
            ?? throw new NotFoundException(nameof(Announcement), id);

        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException();

        // Idempotent: double-POST is a no-op (unique (AnnouncementId, UserId) index backs this up).
        if (await _uow.Announcements.HasReadAsync(announcement.Id, userId)) return;

        await _uow.Announcements.AddReadAsync(new AnnouncementRead
        {
            AnnouncementId = announcement.Id,
            UserId         = userId,
        });
        await _uow.SaveChangesAsync();
    }

    // ── Authoring ────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<AdminAnnouncementDto>> GetAllForAdminAsync() =>
        (await _uow.Announcements.GetAllForAdminAsync()).Select(ToAdminDto);

    public async Task<AdminAnnouncementDto> GetByIdForAdminAsync(int id)
    {
        var announcement = await _uow.Announcements.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Announcement), id);
        return ToAdminDto(announcement);
    }

    public async Task<AdminAnnouncementDto> CreateAsync(SaveAnnouncementDto dto)
    {
        var announcement = new Announcement();
        Apply(announcement, dto);

        await _uow.Announcements.AddAsync(announcement);
        await _uow.SaveChangesAsync();
        return ToAdminDto(announcement);
    }

    public async Task<AdminAnnouncementDto> UpdateAsync(int id, SaveAnnouncementDto dto)
    {
        var announcement = await _uow.Announcements.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Announcement), id);

        // Deliberately does not touch AnnouncementRead rows (D9): fixing a typo must not put the
        // banner back in front of everyone who already dismissed it.
        Apply(announcement, dto);
        announcement.UpdatedAt = DateTime.UtcNow;

        await _uow.Announcements.UpdateAsync(announcement);
        await _uow.SaveChangesAsync();
        return ToAdminDto(announcement);
    }

    public async Task DeleteAsync(int id)
    {
        var announcement = await _uow.Announcements.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Announcement), id);

        await _uow.Announcements.DeleteAsync(announcement);
        await _uow.SaveChangesAsync();
    }

    public async Task<AdminAnnouncementDto> SetPublishedAsync(int id, bool isPublished)
    {
        var announcement = await _uow.Announcements.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Announcement), id);

        if (announcement.IsPublished == isPublished) return ToAdminDto(announcement);

        if (isPublished && string.IsNullOrWhiteSpace(announcement.BodyMarkdown))
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["bodyMarkdown"] = ["Objava mora imati opis prije objavljivanja."]
            });

        // PublishedAt is set on the very first publish only. Unpublishing keeps it, so a
        // re-publish returns to its place in the chronology rather than jumping to the top (D9).
        announcement.IsPublished = isPublished;
        if (isPublished && announcement.PublishedAt is null) announcement.PublishedAt = DateTime.UtcNow;
        announcement.UpdatedAt = DateTime.UtcNow;

        await _uow.Announcements.UpdateAsync(announcement);
        await _uow.SaveChangesAsync();
        return ToAdminDto(announcement);
    }

    // ── Helpers & mapping ────────────────────────────────────────────────────────

    private async Task<HashSet<int>> ReadIdsForCurrentUserAsync() =>
        _currentUser.UserId is int userId
            ? await _uow.Announcements.GetReadIdsAsync(userId)
            : [];

    private async Task<int> UnreadCountForCurrentUserAsync() =>
        _currentUser.UserId is int userId
            ? await _uow.Announcements.GetUnreadCountAsync(userId)
            : 0;

    private static void Apply(Announcement announcement, SaveAnnouncementDto dto)
    {
        announcement.Title        = dto.Title.Trim();
        announcement.Type         = dto.Type;
        announcement.BodyMarkdown = dto.BodyMarkdown;
    }

    private static T MapCommon<T>(T dto, Announcement a, bool isRead) where T : AnnouncementSummaryDto
    {
        dto.Id          = a.Id;
        dto.Title       = a.Title;
        dto.Type        = a.Type;
        dto.TypeName    = BsLabels.Label(a.Type);
        dto.IsRead      = isRead;
        dto.PublishedAt = a.PublishedAt;
        return dto;
    }

    private static AnnouncementSummaryDto ToSummaryDto(Announcement a, HashSet<int> readIds) =>
        MapCommon(new AnnouncementSummaryDto(), a, readIds.Contains(a.Id));

    private static AnnouncementDetailDto ToDetailDto(Announcement a, bool isRead)
    {
        var dto = MapCommon(new AnnouncementDetailDto(), a, isRead);
        dto.BodyMarkdown = a.BodyMarkdown;
        return dto;
    }

    private static AdminAnnouncementDto ToAdminDto(Announcement a) => new()
    {
        Id           = a.Id,
        Title        = a.Title,
        Type         = a.Type,
        TypeName     = BsLabels.Label(a.Type),
        BodyMarkdown = a.BodyMarkdown,
        IsPublished  = a.IsPublished,
        PublishedAt  = a.PublishedAt,
        CreatedAt    = a.CreatedAt,
        UpdatedAt    = a.UpdatedAt,
    };
}
