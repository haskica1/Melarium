using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Learning.DTOs;

/// <summary>
/// The author's own view of a topic they proposed (SPEC-26) — everything they typed, plus where the
/// review got to. Carries the body so the edit form can be filled from one request.
/// </summary>
public class MyLearningSubmissionDto
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

    public TopicReviewStatus ReviewStatus { get; set; }
    public string ReviewStatusName { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Set only while <see cref="ReviewStatus"/> is Rejected — cleared on resubmit.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>True once approved and live in Edukacija; the topic page is then reachable by id.</summary>
    public bool IsPublished { get; set; }

    /// <summary>Whether the author may still edit or withdraw it — false once approved.</summary>
    public bool CanEdit { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
