using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

/// <summary>
/// A single turn inside an <see cref="AiAssistantSession"/> — the user's command or the assistant's
/// reply (SPEC-17). The assistant's turn is what carries the proposed <see cref="Actions"/>.
/// </summary>
public class AiAssistantTurn : BaseEntity
{
    public int SessionId { get; set; }
    public AiAssistantSession Session { get; set; } = null!;

    public AiTurnRole Role { get; set; }

    /// <summary>The user's text, or the assistant's plain-Bosnian reply.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Raw speech-to-text output when the turn came from the microphone; null for typed input.</summary>
    public string? Transcript { get; set; }

    /// <summary>
    /// The model's unmodified JSON envelope, kept for the history page and for diagnosing what the
    /// assistant actually understood (SPEC-17 D8). Assistant turns only.
    /// </summary>
    public string? RawModelJson { get; set; }

    /// <summary>
    /// Serialized <c>AssistantCandidateDto[]</c> — tappable follow-up options the resolver found for an
    /// unresolved apiary/hive (SPEC-17 Phase B, D7). Persisted (not recomputed on read) so re-opening a
    /// session shows the same buttons it showed the first time. Assistant turns only; null when nothing
    /// needed clarifying.
    /// </summary>
    public string? CandidatesJson { get; set; }

    public List<AiAssistantAction> Actions { get; set; } = [];
}
