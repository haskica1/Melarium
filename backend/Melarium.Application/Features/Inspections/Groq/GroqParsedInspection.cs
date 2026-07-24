using System.Text.Json.Serialization;

namespace Melarium.Application.Features.Inspections.Groq;

/// <summary>The JSON object the Groq model is asked to return for an inspection.</summary>
internal sealed class GroqParsedInspection
{
    [JsonPropertyName("date")]          public string? Date        { get; set; }
    [JsonPropertyName("temperature")]   public double? Temperature { get; set; }
    [JsonPropertyName("honeyLevel")]    public int?    HoneyLevel  { get; set; }
    [JsonPropertyName("broodStatus")]   public string? BroodStatus { get; set; }
    [JsonPropertyName("notes")]         public string? Notes       { get; set; }
}
