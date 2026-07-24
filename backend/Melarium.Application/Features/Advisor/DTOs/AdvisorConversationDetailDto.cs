namespace Melarium.Application.Features.Advisor.DTOs;

/// <summary>Full conversation with its ordered messages.</summary>
public record AdvisorConversationDetailDto(
    int Id,
    string Title,
    int? BeehiveId,
    string? BeehiveName,
    DateTime LastMessageAt,
    DateTime CreatedAt,
    IReadOnlyList<AdvisorMessageDto> Messages);
