namespace Melarium.Application.Features.Advisor.DTOs;

/// <summary>List-view conversation summary (no message rows).</summary>
public record AdvisorConversationSummaryDto(
    int Id,
    string Title,
    int? BeehiveId,
    string? BeehiveName,
    DateTime LastMessageAt,
    DateTime CreatedAt);
