namespace Melarium.Application.Features.Learning.DTOs;

public class LearningTopicDetailDto : LearningTopicSummaryDto
{
    public string BodyMarkdown { get; set; } = string.Empty;
}
