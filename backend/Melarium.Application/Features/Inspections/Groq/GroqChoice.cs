using System.Text.Json.Serialization;

namespace Melarium.Application.Features.Inspections.Groq;

internal sealed class GroqChoice
{
    [JsonPropertyName("message")]
    public GroqMessage? Message { get; set; }
}
