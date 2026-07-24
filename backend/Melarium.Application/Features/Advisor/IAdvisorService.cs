using Melarium.Application.Features.Advisor.DTOs;

namespace Melarium.Application.Features.Advisor;

public interface IAdvisorService
{
    Task<IEnumerable<AdvisorConversationSummaryDto>> GetConversationsAsync();
    Task<AdvisorConversationDetailDto> GetConversationAsync(int id);
    Task<AdvisorConversationDetailDto> CreateConversationAsync(CreateConversationDto dto);
    Task<AdvisorMessagePairDto> SendMessageAsync(int conversationId, SendMessageDto dto);
    Task DeleteConversationAsync(int id);

    /// <summary>Transcribes an advisor voice note to text (empty transcript → 400).</summary>
    Task<string> TranscribeAsync(Stream audioStream, string fileName);
}
