using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Feedbacks.DTOs;

/// <summary>
/// A new submission. <c>PageContext</c>/<c>UserAgent</c> are captured by the client automatically —
/// they are debugging hints, never trusted input.
/// </summary>
public record CreateFeedbackDto(
    FeedbackType Type,
    FeedbackSeverity? Severity,
    string Subject,
    string Message,
    string? PageContext,
    string? UserAgent);
