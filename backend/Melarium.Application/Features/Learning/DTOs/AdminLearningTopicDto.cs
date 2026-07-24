using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Learning.DTOs;

/// <summary>Full authoring projection — includes unpublished state and the body.</summary>
public class AdminLearningTopicDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public LearningCategory Category { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int[]? Months { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string BodyMarkdown { get; set; } = string.Empty;
    public string? VideoUrl { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
