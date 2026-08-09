using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Ai;
using Melarium.Application.Features.Inspections.DTOs;
using Melarium.Application.Features.Inspections.Groq;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Features.Inspections;

public class VoiceParsingService : IVoiceParsingService
{
    private readonly HttpClient _http;
    private readonly ITranscriptionService _transcription;
    private readonly ICurrentUser _currentUser;
    private readonly IPlanGuard _plan;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // Keeps Bosnian characters (š, č, ž…) readable in the few-shot examples so the
    // model is taught to emit natural text rather than \uXXXX escapes.
    private static readonly JsonSerializerOptions ExampleJsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // The text moved to BeekeepingPrompt when SPEC-17 needed the same glossary; it is unchanged,
    // and BeekeepingPromptTests locks it so the extraction cannot quietly become a retune.
    private const string SystemMessage = BeekeepingPrompt.VoiceInspectionSystem;

    private const string PromptTemplate =
        """
        Iz sljedećeg glasovnog opisa pregleda košnice izvuci strukturirane podatke.

        TRANSKRIPT:
        "@@TRANSCRIPT@@"

        Današnji datum: @@TODAY@@

        Vrati ISKLJUČIVO validan JSON (bez markdowna, koda ili objašnjenja):
        {
          "date": "<yyyy-MM-dd ili null>",
          "honeyLevel": <1, 2, 3 ili null>,
          "broodStatus": "<kratak opis stanja legla/matice ili null>",
          "notes": "<ostale napomene ili null>"
        }

        PRAVILA:
        1. date — relativne izraze ("danas", "jučer/juče", "prekjučer", dan u sedmici) preračunaj
           prema današnjem datumu. Ako datum nije spomenut → null.
        2. honeyLevel — procijeni količinu meda:
           - 1 (nisko): "nema meda", "prazno", "malo meda", "slabo", "oskudno", "tanko"
           - 2 (srednje): "osrednje", "normalno", "ok", "pola", "dovoljno", "solidno", "umjereno"
           - 3 (visoko): "puno meda", "puni okviri/satovi", "puna medišta", "bogato", "odlično"
           - null: ako se med uopće ne spominje
        3. broodStatus — sažet, standardiziran opis legla i matice; više opažanja spoji zarezom.
           Npr. "Matica uočena, zdravo leglo", "Matica nije uočena", "Jaja i larve prisutni",
           "Lijep kompaktan obrazac legla", "Prisutni matičnjaci". null ako se leglo/matica ne spominju.
        4. notes — sve ostalo: tretmani, dodani/oduzeti okviri i nastavci, hranjenje (sirup, pogača),
           rojenje, jačina i ponašanje društva, vremenski uvjeti, planovi. null ako nema ničeg dodatnog.
        5. Ne ponavljaj isti podatak u više polja (stanje matice ide samo u broodStatus).
        6. Vrati SAMO JSON.
        """;

    public VoiceParsingService(
        HttpClient http,
        IConfiguration config,
        ITranscriptionService transcription,
        ICurrentUser currentUser,
        IPlanGuard plan)
    {
        _http = http;
        _transcription = transcription;
        _currentUser = currentUser;
        _plan = plan;
        var apiKey = config["Groq:ApiKey"] ?? throw new InvalidOperationException("Groq:ApiKey is not configured.");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<ParseVoiceResult> ParseAsync(Stream audioStream, string fileName)
    {
        // Voice input is a paid-plan feature (SPEC-09); org-less callers pass through.
        if (_currentUser.OrganizationId is int orgId)
            await _plan.EnsureFeatureAsync(orgId, PlanFeature.VoiceInput);

        // Transcription is shared with the advisor (ITranscriptionService); this service only
        // adds the inspection-field extraction step on top of the transcript.
        var transcript = await _transcription.TranscribeAsync(audioStream, fileName);
        var fields     = await ExtractFieldsAsync(transcript);
        fields.Transcript = transcript;
        return fields;
    }

    // ── Llama field extraction (few-shot guided) ──────────────────────────────

    private async Task<ParseVoiceResult> ExtractFieldsAsync(string transcript)
    {
        var today     = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");

        var messages = new[]
        {
            new { role = "system",    content = SystemMessage },

            // Few-shot 1 — relative date "danas", high honey, queen seen, treatment + feeding.
            new { role = "user",      content = BuildPrompt(
                "Pregledao sam košnicu danas. Maticu sam lijepo vidio, leglo je zdravo i kompaktno. " +
                "Meda ima podosta, skoro puni okviri. Dodao sam jedan medni nastavak i dao malo šećernog sirupa.",
                today) },
            new { role = "assistant", content = Example(today, 3,
                "Matica uočena, zdravo i kompaktno leglo",
                "Dodan medni nastavak; prihrana šećernim sirupom") },

            // Few-shot 2 — relative date "jučer", no honey, queenless, swarm signs, behaviour.
            new { role = "user",      content = BuildPrompt(
                "Jučer sam bio kod druge košnice. Maticu nisam našao, ali ima dosta matičnjaka " +
                "pa se vjerovatno sprema rojenje. Meda skoro nimalo. Pčele su bile prilično agresivne.",
                today) },
            new { role = "assistant", content = Example(yesterday, 1,
                "Matica nije uočena, prisutni matičnjaci",
                "Moguće rojenje; pčele agresivne") },

            // Few-shot 3 — only feeding mentioned; everything else stays null.
            new { role = "user",      content = BuildPrompt(
                "Samo sam dodao pogaču, nisam stigao otvarati košnicu.", today) },
            new { role = "assistant", content = Example(null, null, null,
                "Dodana pogača (prihrana); košnica nije otvarana") },

            // The real note.
            new { role = "user",      content = BuildPrompt(transcript, today) },
        };

        var requestBody = new
        {
            model           = "llama-3.3-70b-versatile",
            temperature     = 0.0,
            max_tokens      = 512,
            messages,
            response_format = new { type = "json_object" },
        };

        var response = await _http.PostAsJsonAsync(
            "https://api.groq.com/openai/v1/chat/completions", requestBody);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadFromJsonAsync<GroqChatResponse>(JsonOpts)
            ?? throw new InvalidOperationException("Empty response from Groq chat API.");

        var jsonText = raw.Choices?[0].Message?.Content
            ?? throw new InvalidOperationException("No content in Groq chat response.");

        var parsed = JsonSerializer.Deserialize<GroqParsedInspection>(jsonText, JsonOpts)
            ?? throw new InvalidOperationException("Failed to deserialize field extraction result.");

        return new ParseVoiceResult
        {
            Date        = parsed.Date,
            Temperature = parsed.Temperature,
            HoneyLevel  = parsed.HoneyLevel.HasValue ? (HoneyLevel)parsed.HoneyLevel.Value : null,
            BroodStatus = parsed.BroodStatus,
            Notes       = parsed.Notes,
        };
    }

    private static string BuildPrompt(string transcript, string today) => PromptTemplate
        .Replace("@@TRANSCRIPT@@", transcript)
        .Replace("@@TODAY@@", today);

    private static string Example(string? date, int? honeyLevel, string? broodStatus, string notes) =>
        JsonSerializer.Serialize(new { date, honeyLevel, broodStatus, notes }, ExampleJsonOpts);
}
