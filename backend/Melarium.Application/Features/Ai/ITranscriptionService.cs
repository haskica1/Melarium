namespace Melarium.Application.Features.Ai;

/// <summary>
/// Speech-to-text for beekeeping voice notes (BCS). Extracted from <c>VoiceParsingService</c> so it
/// can be reused by both inspection voice parsing and the AI advisor (SPEC-01, ADR-024).
/// </summary>
public interface ITranscriptionService
{
    /// <summary>Transcribes an audio stream to plain text. Throws on transport/API failure.</summary>
    Task<string> TranscribeAsync(Stream audioStream, string fileName);
}
