using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Announcements.DTOs;

/// <summary>Create/update payload. Title and body only (D6) — no image, no CTA link.</summary>
public class SaveAnnouncementDto
{
    public string Title { get; set; } = string.Empty;
    public AnnouncementType Type { get; set; }
    public string BodyMarkdown { get; set; } = string.Empty;
}
