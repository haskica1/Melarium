namespace Melarium.Application.Features.Ai;

/// <summary>
/// Thin wrapper over the Groq chat-completions call for free-form prose replies (as opposed to
/// <see cref="IAssistantAiClient"/>'s constrained JSON envelope). Originally built for the AI advisor
/// (SPEC-01, retired into the assistant by SPEC-18); <c>LearningTopicService</c> also depends on it to
/// draft a topic's Bosnian summary. Isolated behind an interface so callers are unit-testable without
/// hitting the network.
/// </summary>
public interface IProseAiClient
{
    /// <summary>Sends the message list and returns the assistant's plain-text reply. Throws on failure.</summary>
    Task<string> SendAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);
}
