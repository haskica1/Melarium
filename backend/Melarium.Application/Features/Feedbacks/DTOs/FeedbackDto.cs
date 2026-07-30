using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Feedbacks.DTOs;

/// <summary>What the submitter sees about their own report, including the admin's reply if any.</summary>
public record FeedbackDto(
    int Id,
    FeedbackType Type,
    string TypeName,
    FeedbackSeverity? Severity,
    string? SeverityName,
    string Subject,
    string Message,
    string? PageContext,
    bool HasScreenshot,
    FeedbackStatus Status,
    string StatusName,
    string? AdminResponse,
    DateTime? RespondedAt,
    DateTime CreatedAt);
