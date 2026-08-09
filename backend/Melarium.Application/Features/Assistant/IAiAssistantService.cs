using Melarium.Application.Features.Assistant.DTOs;

namespace Melarium.Application.Features.Assistant;

/// <summary>AI assistant: voice/text commands turned into confirmable actions (SPEC-17).</summary>
public interface IAiAssistantService
{
    Task<IEnumerable<AssistantSessionSummaryDto>> GetSessionsAsync();

    /// <summary>404 when the session is not the caller's — never 403, so this is not an existence oracle.</summary>
    Task<AssistantSessionDetailDto> GetSessionAsync(int id);

    Task<AssistantSessionDetailDto> StartSessionAsync(StartAssistantSessionDto dto);

    Task<AssistantSessionDetailDto> AddTurnAsync(int sessionId, AssistantTurnRequestDto dto);

    /// <summary>Executes the listed actions; anything omitted is rejected. Reports per action.</summary>
    Task<AssistantConfirmResponseDto> ConfirmAsync(int turnId, ConfirmActionsDto dto);

    Task RejectAsync(int turnId);

    Task<string> TranscribeAsync(Stream audioStream, string fileName);

    Task DeleteSessionAsync(int id);
}
