namespace Melarium.Application.Features.Advisor.DTOs;

/// <summary>One chat message. <c>Role</c> is "User" or "Assistant".</summary>
public record AdvisorMessageDto(int Id, string Role, string Content, DateTime CreatedAt);
