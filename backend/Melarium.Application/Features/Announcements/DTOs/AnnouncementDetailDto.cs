namespace Melarium.Application.Features.Announcements.DTOs;

/// <summary>A published announcement with its markdown body — what the modal renders.</summary>
public class AnnouncementDetailDto : AnnouncementSummaryDto
{
    public string BodyMarkdown { get; set; } = string.Empty;
}
