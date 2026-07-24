namespace Melarium.Application.Features.Advisor.DTOs;

public class CreateConversationDto
{
    /// <summary>Optional hive to ground the answer in; null = general question.</summary>
    public int? BeehiveId { get; set; }
    public string Message { get; set; } = string.Empty;
}
