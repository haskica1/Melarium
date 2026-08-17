using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Melarium.Application.Common.Exceptions;
using Melarium.Application.Features.Inspections.Groq;
using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Features.Ai;

/// <summary>
/// Groq vision fallback for the "scan by number" flow: reads the number/label painted on a hive when
/// on-device OCR is not confident. Reuses the same OpenAI-compatible payload, base64 image transport,
/// 4 MB cap and forced-JSON output as <see cref="GroqPhotoAnalysisAiClient"/>. Model id lives in config
/// (<c>Groq:VisionModel</c>).
/// </summary>
public class GroqHiveNumberOcrClient : IHiveNumberOcrClient
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly string _model;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // Groq rejects requests whose base64-encoded image exceeds 4 MB (HTTP 413) — reject early.
    private const long MaxBase64Bytes = 4 * 1024 * 1024;

    private const string SystemMessage =
        """
        Ti si sistem za očitavanje broja/oznake sa fotografije košnice.
        Na košnicama je obično naslikan ili napisan broj (npr. "1", "12", "A3").
        Vrati ISKLJUČIVO taj broj/oznaku. Ništa ne izmišljaj; ako broj nije jasno vidljiv, vrati null.
        """;

    private const string UserPrompt =
        """
        Pogledaj fotografiju i vrati ISKLJUČIVO validan JSON (bez markdowna, koda ili objašnjenja):
        {
          "number": "očitani broj ili oznaka (npr. 7 ili A3)" ili null,
          "confidence": broj između 0 i 1
        }
        Ako je vidljivo više brojeva, vrati onaj najveći/najistaknutiji koji predstavlja oznaku košnice.
        Ako nema jasnog broja, number = null, confidence = 0.
        """;

    // Unlike the frame-analysis client this one is injected into BeehiveService (used by every
    // beehive endpoint), so the ctor must never throw on a missing key — otherwise a missing
    // Groq:ApiKey would break plain beehive CRUD. The key is validated lazily at call time instead.
    public GroqHiveNumberOcrClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["Groq:ApiKey"];
        if (!string.IsNullOrWhiteSpace(_apiKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _model = GroqModels.Vision(config);
    }

    public async Task<HiveNumberOcrResult> RecognizeNumberAsync(byte[] imageBytes, string contentType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new BusinessRuleException(
                "AI prepoznavanje broja trenutno nije dostupno. Pokušajte QR kod ili ručni unos.");

        var base64 = Convert.ToBase64String(imageBytes);
        if (base64.Length > MaxBase64Bytes)
            throw new BusinessRuleException(
                "Fotografija je prevelika za AI prepoznavanje (najviše oko 3 MB). Pokušajte s manjom fotografijom.");

        var requestBody = new
        {
            model = _model,
            temperature = 0.0,
            max_tokens = 128,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = SystemMessage },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = UserPrompt },
                        new { type = "image_url", image_url = new { url = $"data:{contentType};base64,{base64}" } },
                    },
                },
            },
        };

        var response = await _http.PostAsJsonAsync(
            "https://api.groq.com/openai/v1/chat/completions", requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadFromJsonAsync<GroqChatResponse>(JsonOpts, cancellationToken)
            ?? throw new InvalidOperationException("Empty response from Groq chat API.");

        var jsonText = raw.Choices?[0].Message?.Content
            ?? throw new InvalidOperationException("No content in Groq chat response.");

        return Parse(jsonText);
    }

    /// <summary>
    /// Parses the model output. Tolerant of <c>number</c> arriving as a JSON string or a JSON number;
    /// malformed JSON yields an empty (no-number) result rather than throwing — the scan just finds nothing.
    /// </summary>
    public static HiveNumberOcrResult Parse(string jsonText)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new HiveNumberOcrResult(null, null);

            string? number = null;
            if (root.TryGetProperty("number", out var numEl))
            {
                number = numEl.ValueKind switch
                {
                    JsonValueKind.String => numEl.GetString(),
                    JsonValueKind.Number => numEl.ToString(),
                    _ => null,
                };
            }
            number = string.IsNullOrWhiteSpace(number) ? null : number.Trim();

            double? confidence = null;
            if (root.TryGetProperty("confidence", out var confEl) && confEl.ValueKind == JsonValueKind.Number)
                confidence = confEl.GetDouble();

            return new HiveNumberOcrResult(number, confidence);
        }
        catch (JsonException)
        {
            return new HiveNumberOcrResult(null, null);
        }
    }
}
