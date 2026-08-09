namespace Melarium.Application.Features.Ai;

/// <summary>
/// Groq chat-completions call for the AI assistant (SPEC-17), constrained to a JSON object response.
/// Behind an interface so <c>AiAssistantService</c> is unit-testable without the network — the same
/// reason <see cref="IAdvisorAiClient"/> exists.
/// </summary>
public interface IAssistantAiClient
{
    /// <summary>Sends the messages and returns the raw JSON envelope text. Throws on failure.</summary>
    Task<string> SendAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);
}
