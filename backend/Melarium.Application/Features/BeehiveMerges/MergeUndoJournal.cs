using System.Text.Json;
using System.Text.Json.Serialization;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.BeehiveMerges;

/// <summary>
/// Everything a merge changed outside <c>BeehiveMerges</c>, captured at merge time so the 24-hour
/// undo (SPEC-19 §4) can put it back exactly. Two of these cannot be reconstructed from the database
/// afterwards at all: the deleted todos, and each queen's status before it was closed.
///
/// <para>Stored as JSON on <c>BeehiveMerge.UndoJournalJson</c> — same approach as
/// <c>AiActionPayload.PreviousFields</c> (SPEC-17), and for the same reason: a snapshot of prior
/// state belongs with the event that replaced it, not in a table of its own.</para>
/// </summary>
public sealed record MergeUndoJournal(
    IReadOnlyList<JournalQueen> Queens,
    IReadOnlyList<JournalTodo> Todos,
    IReadOnlyList<JournalDietHive> DietHives,
    IReadOnlyList<JournalTreatmentEntry> Treatments)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>Reads a stored journal; null rather than throwing on a row written by older code.</summary>
    public static MergeUndoJournal? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<MergeUndoJournal>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>A queen as she was before the merge closed or moved her.</summary>
public sealed record JournalQueen(
    int Id,
    int BeehiveId,
    QueenStatus Status,
    DateTime? EndDate,
    string? Notes);

/// <summary>
/// A deleted todo, by value. The restored row gets a new <c>Id</c> — nothing points at a todo id, so
/// that is the one thing the undo cannot and need not reproduce.
/// </summary>
public sealed record JournalTodo(
    string Title,
    string? Notes,
    DateTime? DueDate,
    TodoPriority Priority,
    int? AssignedToId,
    int? CreatedById,
    DateTime CreatedAt);

/// <summary>
/// A feeding-programme membership that was closed. Restoring clears <c>RemovedOn</c> on the
/// <b>same</b> row: a fresh row would carry a new <c>CreatedAt</c>, and that column is "when the hive
/// joined the programme", which the consumption maths reads.
/// </summary>
public sealed record JournalDietHive(
    int DietBeehiveId,
    int DietId,
    /// <summary>True when this merge also stopped the whole programme early (last hive on it).</summary>
    bool CompletedEarly,
    DietStatus PreviousStatus,
    string? PreviousEarlyCompletionComment);

/// <summary>
/// A treatment line whose <c>DoseNote</c> the merge appended to. The treatment id is carried because
/// entries have no repository of their own — they are reached through their parent.
/// </summary>
public sealed record JournalTreatmentEntry(
    int TreatmentId,
    int TreatmentEntryId,
    string? DoseNote);
