using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Melarium.Application.Features.Inspections.Groq;
using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Features.Ai;

/// <summary>
/// Groq chat-completions client for free-form prose (<see cref="GroqModels.Chat"/>, plain text). Same
/// endpoint and auth pattern as inspection voice parsing; no `response_format` — the reply is whatever
/// prose the caller's prompt asks for (Bosnian advice, a learning-topic summary, ...).
/// </summary>
public class GroqProseAiClient : IProseAiClient
{
    private readonly HttpClient _http;
    private readonly string _model;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public GroqProseAiClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _model = GroqModels.Chat(config);
        var apiKey = config["Groq:ApiKey"] ?? throw new InvalidOperationException("Groq:ApiKey is not configured.");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> SendAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var requestBody = new
        {
            model = _model,
            temperature = 0.4,
            max_tokens = 1024,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
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
