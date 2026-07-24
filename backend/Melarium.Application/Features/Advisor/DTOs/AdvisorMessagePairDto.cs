namespace Melarium.Application.Features.Advisor.DTOs;

/// <summary>The user message + assistant reply appended by a single send.</summary>
public record AdvisorMessagePairDto(AdvisorMessageDto UserMessage, AdvisorMessageDto AssistantMessage);
