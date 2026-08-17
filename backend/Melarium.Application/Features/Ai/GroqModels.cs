using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Features.Ai;

/// <summary>
/// Which Groq model each kind of call uses — in one place, and overridable from configuration.
///
/// <para>Groq retires models on a schedule and answers a retired id with a plain
/// <c>404 model_not_found</c>. On 2026-08-17 <c>llama-3.3-70b-versatile</c> stopped being served and
/// took four features down at once (assistant, voice-note parsing, weekly summary, learning drafts),
/// because its id was pasted into each of their request bodies. The point of this class is that the
/// next retirement is one line in <c>.env</c>, not a code change and a redeploy per feature.</para>
///
/// <para>Whatever replaces <see cref="DefaultChat"/> must support <c>response_format:
/// json_object</c> — the assistant and voice parsing both depend on JSON mode — and whatever
/// replaces <see cref="DefaultVision"/> must accept image content.</para>
/// </summary>
public static class GroqModels
{
    public const string ChatConfigKey   = "Groq:ChatModel";
    public const string VisionConfigKey = "Groq:VisionModel";

    /// <summary>Groq's own recommended migration target for the retired Llama 3.3 70B.</summary>
    public const string DefaultChat = "openai/gpt-oss-120b";

    /// <summary>
    /// Replaces Llama 4 Scout, retired 2026-07-17. Chosen because it is the only model Groq still
    /// serves that takes image input at all — and it happens to cover what both image callers need:
    /// JSON mode alongside an image, a system message, and up to 5 images per request (we send one).
    /// </summary>
    public const string DefaultVision = "qwen/qwen3.6-27b";

    /// <summary>Text/JSON completions: assistant, voice parsing, weekly summary, prose replies.</summary>
    public static string Chat(IConfiguration config) =>
        Trimmed(config[ChatConfigKey]) ?? DefaultChat;

    /// <summary>Image completions: frame photo analysis and the hive-number OCR fallback.</summary>
    public static string Vision(IConfiguration config) =>
        Trimmed(config[VisionConfigKey]) ?? DefaultVision;

    // An empty value in appsettings/.env means "not set", never "use the empty model id" — that
    // would turn a blank line in a config file into a 404 from Groq.
    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
