namespace Melarium.Application.Features.Ai;

/// <summary>One message in a chat-completions request. <paramref name="Role"/> is "system", "user", or "assistant".</summary>
public record ChatMessage(string Role, string Content);
