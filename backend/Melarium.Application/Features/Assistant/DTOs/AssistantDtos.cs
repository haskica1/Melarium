using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Assistant.DTOs;

// ── Requests ──────────────────────────────────────────────────────────────────

/// <summary>Starts a session. <c>ApiaryId</c>/<c>BeehiveId</c> are the page the user is on; they fill gaps only.</summary>
public class StartAssistantSessionDto
{
    public string Text { get; set; } = string.Empty;

    /// <summary>Raw speech-to-text output when the text came from the microphone, kept for the history (D8).</summary>
    public string? Transcript { get; set; }

    public int? ApiaryId { get; set; }
    public int? BeehiveId { get; set; }
}

/// <summary>Appends a turn to an existing session.</summary>
public class AssistantTurnRequestDto
{
    public string Text { get; set; } = string.Empty;

    /// <summary>Raw speech-to-text output when the text came from the microphone, kept for the history (D8).</summary>
    public string? Transcript { get; set; }

    public int? ApiaryId { get; set; }
    public int? BeehiveId { get; set; }
}

/// <summary>
/// The confirmation. Everything here is <b>untrusted</b>: the card is editable, so the client may send
/// any id or value, and the executor re-validates and re-checks access exactly as for a form post.
/// Actions omitted from the list are rejected.
/// </summary>
public class ConfirmActionsDto
{
    public List<ConfirmActionItemDto> Actions { get; set; } = [];
}

public class ConfirmActionItemDto
{
    public int Id { get; set; }
    public int? ApiaryId { get; set; }
    public int? BeehiveId { get; set; }
    public AssistantFieldsDto Fields { get; set; } = new();
}

/// <summary>Editable action fields, as the card holds them.</summary>
public class AssistantFieldsDto
{
    public DateOnly? Date { get; set; }
    public HoneyLevel? HoneyLevel { get; set; }
    public string? BroodStatus { get; set; }
    public string? Notes { get; set; }
    public string? Title { get; set; }
    public TodoPriority? Priority { get; set; }
    public DateOnly? DueDate { get; set; }
}

// ── Responses ─────────────────────────────────────────────────────────────────

public record AssistantSessionSummaryDto(
    int Id, string Title, DateTime LastActivityAt, DateTime CreatedAt, int? BeehiveId, string? BeehiveName);

public record AssistantSessionDetailDto(
    int Id,
    string Title,
    DateTime LastActivityAt,
    DateTime CreatedAt,
    int? BeehiveId,
    string? BeehiveName,
    IReadOnlyList<AssistantTurnDto> Turns);

/// <summary>
/// <c>Role</c> is "User" or "Assistant". <c>Candidates</c> (SPEC-17 Phase B) is non-empty only on the
/// latest assistant turn when something needed clarifying — tapping one sends its <c>text</c> as the
/// next turn, exactly like typing it. Always empty once a newer turn exists.
/// </summary>
public record AssistantTurnDto(
    int Id,
    string Role,
    string Content,
    string? Transcript,
    DateTime CreatedAt,
    IReadOnlyList<AssistantActionDto> Actions,
    IReadOnlyList<AssistantCandidateDto> Candidates);

/// <summary>One tappable follow-up option. <c>Label</c> is shown on the button, <c>Text</c> is sent when tapped.</summary>
public record AssistantCandidateDto(string Label, string Text);

/// <summary>
/// One proposal card. <c>Issue</c> is "None" when the target resolved; anything else means the card
/// needs the user to complete it before it can be confirmed.
///
/// <c>IsDestructive</c> (Phase C, SPEC-17 D5/§7.3) marks update/complete/delete kinds — a batch
/// containing any of these needs a second, separate confirmation on top of the first. The apiary/hive
/// on such a card are the EXISTING record's own (display only, not re-pickable); <c>PreviousFields</c>
/// is that record's current values, so the card can show "prije → poslije" without a second fetch.
/// </summary>
public record AssistantActionDto(
    int Id,
    string Kind,
    string KindLabel,
    string Status,
    string TargetSummary,
    string Issue,
    IReadOnlyList<string> UnresolvedHiveNumbers,
    int? ApiaryId,
    string? ApiaryName,
    int? BeehiveId,
    string? BeehiveName,
    AssistantFieldsDto Fields,
    string? ResultEntityType,
    int? ResultEntityId,
    string? ErrorMessage,
    bool IsDestructive,
    AssistantFieldsDto? PreviousFields);

/// <summary>Outcome of one confirmed action — honest per action, because a batch can partly fail.</summary>
public record AssistantExecutionResultDto(
    int ActionId,
    string Status,
    string TargetSummary,
    string? ResultEntityType,
    int? ResultEntityId,
    string? ErrorMessage);

/// <summary>What a confirmation produced, plus a Bosnian summary line for the thread.</summary>
public record AssistantConfirmResponseDto(
    string Message,
    IReadOnlyList<AssistantExecutionResultDto> Results);

public record AssistantTranscriptDto(string Transcript);
