using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

/// <summary>
/// One educational article (SPEC-06). Platform-wide content: authored by SystemAdmin, or proposed by
/// a user and approved by one (SPEC-26); visible to all organizations once published.
/// <see cref="Months"/> marks when the topic is seasonal ("aktuelno"); null/empty = evergreen.
/// </summary>
public class LearningTopic : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public LearningCategory Category { get; set; }

    /// <summary>Months (1–12) when the topic is current; null = evergreen (Postgres integer[]).</summary>
    public int[]? Months { get; set; }

    /// <summary>Card teaser shown in the list.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>The article body, markdown.</summary>
    public string BodyMarkdown { get; set; } = string.Empty;

    /// <summary>Optional video link (YouTube/Vimeo/direct video file) shown on the topic page.</summary>
    public string? VideoUrl { get; set; }

    /// <summary>Optional link to an attached file (e.g. a hosted PDF).</summary>
    public string? FileUrl { get; set; }

    /// <summary>Display label for <see cref="FileUrl"/> — falls back to a generic label when empty.</summary>
    public string? FileName { get; set; }

    public bool IsPublished { get; set; }

    /// <summary>Set on the first publish only — the once-per-topic notification guard.</summary>
    public DateTime? PublishedAt { get; set; }

    // ── User submissions (SPEC-26) ───────────────────────────────────────────────

    /// <summary>
    /// The user who proposed the topic; null for SystemAdmin-authored content. <c>ON DELETE SET NULL</c>
    /// — a deleted account leaves the published article standing, only unattributed.
    /// </summary>
    public int? AuthorId { get; set; }
    public User? Author { get; set; }

    /// <summary>Review state. <see cref="TopicReviewStatus.None"/> = admin-authored, never reviewed.</summary>
    public TopicReviewStatus ReviewStatus { get; set; } = TopicReviewStatus.None;

    /// <summary>When the author last sent it in — reset on every resubmit after a rejection.</summary>
    public DateTime? SubmittedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedById { get; set; }
    public User? ReviewedBy { get; set; }

    /// <summary>Why the topic was rejected — shown to the author. Cleared when they resubmit.</summary>
    public string? RejectionReason { get; set; }

    public List<LearningTopicRead> Reads { get; set; } = [];
}
