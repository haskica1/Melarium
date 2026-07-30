using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Feedbacks.DTOs;

/// <summary>
/// The admin view: everything the submitter sees plus who sent it and from which organisation.
/// Submitter fields are null when the account has since been deleted — the report survives it.
/// </summary>
public record AdminFeedbackDto(
    int Id,
    FeedbackType Type,
    string TypeName,
    FeedbackSeverity? Severity,
    string? SeverityName,
    string Subject,
    string Message,
    string? PageContext,
    string? UserAgent,
    bool HasScreenshot,
    FeedbackStatus Status,
    string StatusName,
    string? AdminResponse,
    DateTime? RespondedAt,
    DateTime CreatedAt,
    int? UserId,
    string? SubmitterName,
    string? SubmitterEmail,
    string? OrganizationName);
