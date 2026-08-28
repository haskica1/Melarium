using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class AnnouncementRepository : Repository<Announcement>, IAnnouncementRepository
{
    public AnnouncementRepository(MelariumDbContext context) : base(context) { }

    public async Task<IEnumerable<Announcement>> GetPublishedAsync(AnnouncementType? type = null) =>
        await PublishedNewestFirst()
            .Where(a => type == null || a.Type == type)
            .ToListAsync();

    public async Task<Announcement?> GetLatestPublishedAsync() =>
        await PublishedNewestFirst().FirstOrDefaultAsync();

    public async Task<Announcement?> GetPublishedByIdAsync(int id) =>
        await _context.Announcements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.IsPublished);

    public async Task<IEnumerable<Announcement>> GetAllForAdminAsync() =>
        await _context.Announcements
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

    public async Task<HashSet<int>> GetReadIdsAsync(int userId)
    {
        var ids = await _context.Set<AnnouncementRead>()
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .Select(r => r.AnnouncementId)
            .ToListAsync();
        return [.. ids];
    }

    public async Task<int> GetUnreadCountAsync(int userId) =>
        await _context.Announcements
            .AsNoTracking()
            .Where(a => a.IsPublished)
            .CountAsync(a => !_context.Set<AnnouncementRead>()
                .Any(r => r.AnnouncementId == a.Id && r.UserId == userId));

    public async Task<bool> HasReadAsync(int announcementId, int userId) =>
        await _context.Set<AnnouncementRead>()
            .AsNoTracking()
            .AnyAsync(r => r.AnnouncementId == announcementId && r.UserId == userId);

    public async Task AddReadAsync(AnnouncementRead read) =>
        await _context.Set<AnnouncementRead>().AddAsync(read);

    // PublishedAt, not CreatedAt (D10) — a draft written in January and published in March belongs
    // to March. Id breaks ties so two announcements published in the same instant keep a stable order.
    private IQueryable<Announcement> PublishedNewestFirst() =>
        _context.Announcements
            .AsNoTracking()
            .Where(a => a.IsPublished)
            .OrderByDescending(a => a.PublishedAt)
            .ThenByDescending(a => a.Id);
}
