using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Features.Ai;

/// <summary>
/// Groq Whisper large-v3 transcription (BCS). Moved verbatim out of <c>VoiceParsingService</c> so the
/// same transcription is shared by inspection voice parsing and the advisor's <c>/transcribe</c>.
/// </summary>
public class GroqTranscriptionService : ITranscriptionService
{
    private readonly HttpClient _http;

    // Biases Whisper toward the correct spelling of beekeeping terms in BCS.
    private const string TranscriptionPrompt =
        "Glasovna bilješka pčelara o pregledu košnice. Termini: matica, leglo, jaja, larve, " +
        "poklopljeno leglo, okviri, satovi, saće, med, medište, medni nastavak, superica, " +
        "roj, rojenje, matičnjaci, propolis, vosak, pelud, varoa, nozemoza, gnjiloća, " +
        "oksalna kiselina, amitraz, šećerni sirup, pogača.";

    public GroqTranscriptionService(HttpClient http, IConfiguration config)
    {
        _http = http;
        var apiKey = config["Groq:ApiKey"] ?? throw new InvalidOperationException("Groq:ApiKey is not configured.");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> TranscribeAsync(Stream audioStream, string fileName)
    {
        using var content = new MultipartFormDataContent();

        var fileContent = new StreamContent(audioStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(fileName));
        content.Add(fileContent, "file", fileName);
        // Full large-v3 (not turbo) — noticeably more accurate for Bosnian/Croatian/Serbian.
        content.Add(new StringContent("whisper-large-v3"), "model");
        content.Add(new StringContent("bs"), "language");
        content.Add(new StringContent("text"), "response_format");
        content.Add(new StringContent("0"), "temperature");
        content.Add(new StringContent(TranscriptionPrompt), "prompt");

        var response = await _http.PostAsync(
            "https://api.groq.com/openai/v1/audio/transcriptions", content);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadAsStringAsync()).Trim();
    }

    private static string GetMimeType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".webm" => "audio/webm",
            ".mp4"  => "audio/mp4",
            ".m4a"  => "audio/mp4",
            ".ogg"  => "audio/ogg",
            ".wav"  => "audio/wav",
            ".mp3"  => "audio/mpeg",
            _       => "audio/webm",
        };
}
