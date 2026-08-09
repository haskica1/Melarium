using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Melarium.Application.Features.Inspections.Groq;
using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Features.Ai;

/// <summary>
/// Groq chat-completions client for the AI assistant (SPEC-17). Same endpoint and auth as the advisor
/// and voice parsing; differs in asking for <c>response_format: json_object</c> at temperature 0,
/// because the reply is a machine-read envelope rather than prose. Model from
/// <c>Groq:AssistantModel</c>, defaulting to the same Llama the rest of the app's text features use.
/// </summary>
public class GroqAssistantAiClient : IAssistantAiClient
{
    private readonly HttpClient _http;
    private readonly string _model;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public GroqAssistantAiClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        var apiKey = config["Groq:ApiKey"] ?? throw new InvalidOperationException("Groq:ApiKey is not configured.");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _model = config["Groq:AssistantModel"] ?? "llama-3.3-70b-versatile";
    }

    public async Task<string> SendAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var requestBody = new
        {
            model           = _model,
            temperature     = 0.0,
            max_tokens      = 1500,
            messages        = messages.Select(m => new { role = m.Role, content = m.Content }),
            response_format = new { type = "json_object" },
        };

        var response = await _http.PostAsJsonAsync(
            "https://api.groq.com/openai/v1/chat/completions", requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadFromJsonAsync<GroqChatResponse>(JsonOpts, cancellationToken)
            ?? throw new InvalidOperationException("Empty response from Groq chat API.");

        return raw.Choices?[0].Message?.Content?.Trim()
            ?? throw new InvalidOperationException("No content in Groq chat response.");
    }
}
