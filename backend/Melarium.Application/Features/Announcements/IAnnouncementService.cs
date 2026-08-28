using Melarium.Application.Features.Announcements.DTOs;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Announcements;

public interface IAnnouncementService
{
    // ── Consumption ──
    Task<AnnouncementListDto> GetPublishedAsync(AnnouncementType? type);
    Task<AnnouncementBannerDto> GetBannerAsync();
    Task<AnnouncementDetailDto> GetPublishedByIdAsync(int id);
    Task MarkReadAsync(int id);

    // ── Authoring (SystemAdmin) ──
    Task<IEnumerable<AdminAnnouncementDto>> GetAllForAdminAsync();
    Task<AdminAnnouncementDto> GetByIdForAdminAsync(int id);
    Task<AdminAnnouncementDto> CreateAsync(SaveAnnouncementDto dto);
    Task<AdminAnnouncementDto> UpdateAsync(int id, SaveAnnouncementDto dto);
    Task DeleteAsync(int id);
    Task<AdminAnnouncementDto> SetPublishedAsync(int id, bool isPublished);
}
