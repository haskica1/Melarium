namespace Melarium.Application.Features.Ai;

/// <summary>
/// Thin wrapper over the Groq chat-completions call used by the AI advisor. Isolated behind an
/// interface so <c>AdvisorService</c> is unit-testable without hitting the network (SPEC-01).
/// </summary>
public interface IAdvisorAiClient
{
    /// <summary>Sends the message list and returns the assistant's plain-text reply. Throws on failure.</summary>
    Task<string> SendAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);
}
