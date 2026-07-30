using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

/// <summary>
/// One message from a user to the platform operator (SPEC-13) — bug, complaint, compliment,
/// feature idea or question.
/// </summary>
/// <remarks>
/// Not tenant-scoped: this is a channel between one user and SystemAdmin, so it does not go through
/// <c>IAccessGuard</c>. <see cref="UserId"/> and <see cref="OrganizationId"/> are nullable with
/// <c>ON DELETE SET NULL</c> so deleting an account or an organisation keeps the report itself —
/// losing the attribution is acceptable, losing the reported defect is not.
/// </remarks>
public class Feedback : BaseEntity
{
    public int? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Snapshot of the submitter's organisation, for triage context. Null for SystemAdmin.</summary>
    public int? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public FeedbackType Type { get; set; }

    public FeedbackSeverity? Severity { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The route the user was on, captured by the client. A debugging hint only — never trusted for
    /// authorization and never parsed as a resource reference.
    /// </summary>
    public string? PageContext { get; set; }

    public string? UserAgent { get; set; }

    /// <summary>Opaque <c>IFileStorage</c> key for the optional screenshot; null when none was attached.</summary>
    public string? ScreenshotStoragePath { get; set; }

    /// <summary>Real content type of the screenshot, detected from header bytes at upload.</summary>
    public string? ScreenshotContentType { get; set; }

    public FeedbackStatus Status { get; set; } = FeedbackStatus.New;

    public string? AdminResponse { get; set; }

    public DateTime? RespondedAt { get; set; }

    public int? RespondedById { get; set; }
    public User? RespondedBy { get; set; }
}
