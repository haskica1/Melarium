namespace Melarium.Application.Features.Learning.DTOs;

/// <summary>Rejection payload — the reason is mandatory and goes to the author verbatim.</summary>
public class RejectLearningTopicDto
{
    public string Reason { get; set; } = string.Empty;
}
