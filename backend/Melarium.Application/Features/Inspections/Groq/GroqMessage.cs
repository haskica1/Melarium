using System.Text.Json.Serialization;

namespace Melarium.Application.Features.Inspections.Groq;

internal sealed class GroqMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
