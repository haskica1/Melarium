using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Feedbacks.DTOs;

/// <summary>SystemAdmin triage action: move the status and optionally leave a reply.</summary>
public record UpdateFeedbackStatusDto(FeedbackStatus Status, string? AdminResponse);
