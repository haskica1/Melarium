using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Learning.DTOs;

/// <summary>List-card projection of a published topic, with the caller's read flag.</summary>
public class LearningTopicSummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public LearningCategory Category { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int[]? Months { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? VideoUrl { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public bool IsRead { get; set; }
    public DateTime? PublishedAt { get; set; }
}
