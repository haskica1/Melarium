using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Learning.DTOs;

/// <summary>Create/update payload. Publishing is a separate toggle; a draft may have an empty body.</summary>
public class SaveLearningTopicDto
{
    public string Title { get; set; } = string.Empty;
    public LearningCategory Category { get; set; }
    public int[]? Months { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string BodyMarkdown { get; set; } = string.Empty;
    public string? VideoUrl { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
}
