using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Announcements.DTOs;

/// <summary>List projection of a published announcement, with the caller's seen flag.</summary>
public class AnnouncementSummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public AnnouncementType Type { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? PublishedAt { get; set; }
}
