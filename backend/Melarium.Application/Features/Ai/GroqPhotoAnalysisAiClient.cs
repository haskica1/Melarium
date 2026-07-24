using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Melarium.Application.Common.Exceptions;
using Melarium.Application.Features.Inspections.Groq;
using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Features.Ai;

/// <summary>
/// Groq multimodal (vision) client for frame photo analysis. Model id lives in config
/// (<c>Groq:VisionModel</c>, default Llama 4 Scout — Groq's recommended vision model);
/// the image travels base64 in the OpenAI-compatible chat payload, JSON output forced.
/// </summary>
public class GroqPhotoAnalysisAiClient : IPhotoAnalysisAiClient
{
    private readonly HttpClient _http;
    private readonly string _model;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // Groq rejects requests whose base64-encoded image exceeds 4 MB (HTTP 413) — reject
    // early with a Bosnian message instead of burning a request. Base64 inflates by 4/3.
    private const long MaxBase64Bytes = 4 * 1024 * 1024;

    private const string SystemMessage =
        """
        Ti si iskusni pčelarski stručnjak koji analizira fotografije okvira (ramova) saća iz košnice.

        Pravila:
        - Analiziraj ISKLJUČIVO ono što se stvarno vidi na fotografiji; ništa ne izmišljaj.
        - NIKAD ne postavljaj dijagnozu bolesti. Anomalije formuliši oprezno kao opažanja,
          npr. "moguće krečno leglo", "nepravilan obrazac legla", "vidljive deformisane pčele".
        - Sve tekstualne vrijednosti piši na bosanskom jeziku.
        - Ako fotografija NIJE okvir/saće iz košnice (npr. auto, pejzaž, osoba),
          postavi isFramePhoto na false, a SVA ostala polja na null odnosno praznu listu.
        """;

    private const string UserPrompt =
        """
        Analiziraj priloženu fotografiju okvira saća i vrati ISKLJUČIVO validan JSON
        (bez markdowna, koda ili objašnjenja) u tačno ovom obliku:
        {
          "isFramePhoto": true ili false,
          "broodPattern": broj 1-5 ili null,
          "queenCellsVisible": true, false ili null,
          "anomalies": ["kratke fraze na bosanskom"] ili [],
          "summary": "2-3 rečenice na bosanskom ili null"
        }

        PRAVILA:
        1. isFramePhoto — false ako fotografija ne prikazuje okvir/saće iz košnice; tada su sva ostala polja null odnosno [].
        2. broodPattern — ocjena kompaktnosti i pokrivenosti legla: 1 (vrlo loše, raštrkano) do 5 (odlično, kompaktno).
           null ako se leglo ne vidi dovoljno jasno.
        3. queenCellsVisible — true samo ako se matičnjaci jasno vide; null ako se ne može procijeniti.
        4. anomalies — kratka opažanja o mogućim problemima (oprezno, "moguće ..."); [] ako nema ničeg neobičnog.
        5. summary — sažetak stanja u 2-3 rečenice na bosanskom.
        """;

    public GroqPhotoAnalysisAiClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        var apiKey = config["Groq:ApiKey"] ?? throw new InvalidOperationException("Groq:ApiKey is not configured.");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _model = config["Groq:VisionModel"] ?? "meta-llama/llama-4-scout-17b-16e-instruct";
    }

    public async Task<PhotoAnalysisResult> AnalyzeFrameAsync(byte[] imageBytes, string contentType, CancellationToken cancellationToken = default)
    {
        var base64 = Convert.ToBase64String(imageBytes);
        if (base64.Length > MaxBase64Bytes)
            throw new BusinessRuleException(
                "Fotografija je prevelika za AI analizu (najviše oko 3 MB). Pokušajte s manjom fotografijom.");

        var requestBody = new
        {
            model = _model,
            temperature = 0.0,
            max_tokens = 1024,
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

        return ParseAnalysis(jsonText);
    }

    /// <summary>
    /// Parses and normalizes the model output. Malformed JSON becomes a Bosnian
    /// <see cref="BusinessRuleException"/> — the photo itself is never affected.
    /// </summary>
    public static PhotoAnalysisResult ParseAnalysis(string jsonText)
    {
        RawAnalysis? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<RawAnalysis>(jsonText, JsonOpts);
        }
        catch (JsonException)
        {
            parsed = null;
        }

        if (parsed is null)
            throw new BusinessRuleException("AI analiza nije uspjela — model je vratio neispravan odgovor. Pokušajte ponovo.");

        if (!parsed.IsFramePhoto)
        {
            // Non-frame photo: everything except the flag (and optional summary) stays empty.
            return new PhotoAnalysisResult { IsFramePhoto = false, Summary = parsed.Summary };
        }

        return new PhotoAnalysisResult
        {
            IsFramePhoto = true,
            // Out-of-range scores are dropped rather than trusted.
            BroodPattern = parsed.BroodPattern is >= 1 and <= 5 ? parsed.BroodPattern : null,
            QueenCellsVisible = parsed.QueenCellsVisible,
            Anomalies = parsed.Anomalies?.Where(a => !string.IsNullOrWhiteSpace(a)).ToList() ?? [],
            Summary = parsed.Summary,
        };
    }

    /// <summary>Tolerant deserialization target for the raw model output.</summary>
    public class RawAnalysis
    {
        public bool IsFramePhoto { get; set; }
        public int? BroodPattern { get; set; }
        public bool? QueenCellsVisible { get; set; }
        public List<string>? Anomalies { get; set; }
        public string? Summary { get; set; }
    }
}
