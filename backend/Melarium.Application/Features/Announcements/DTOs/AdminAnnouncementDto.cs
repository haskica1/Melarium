using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Announcements.DTOs;

/// <summary>Full authoring projection — includes draft state and the body.</summary>
public class AdminAnnouncementDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public AnnouncementType Type { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string BodyMarkdown { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
