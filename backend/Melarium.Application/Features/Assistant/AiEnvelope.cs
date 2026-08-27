using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Assistant;

/// <summary>
/// The model's parsed reply for one turn (SPEC-17 §3). Produced by <see cref="AiEnvelopeParser"/>;
/// contains only what the model said, with no entity ids — resolution to real apiaries and hives is
/// <see cref="AiTargetResolver"/>'s job.
/// </summary>
public sealed record AiEnvelope(
    string Reply,
    AiClarification? Needs,
    IReadOnlyList<AiProposedAction> Actions);

/// <summary>A follow-up question the assistant needs answered before it can propose anything.</summary>
/// <param name="What">Which field is missing: <c>apiary</c>, <c>beehive</c>, <c>title</c>, <c>date</c>.</param>
public sealed record AiClarification(string What, string Question);

/// <summary>
/// One action as the model described it — names and numbers as spoken, never ids.
/// <paramref name="TargetTitle"/> identifies an EXISTING todo for the Phase C update/complete/delete
/// kinds (matched by title, per SPEC-17 §9); existing inspections are identified by
/// <see cref="AiActionFields.Date"/> instead — a hive already has at most one inspection most days,
/// where a todo has no comparable natural key.
/// </summary>
public sealed record AiProposedAction(
    AiActionKind Kind,
    string? Apiary,
    AiHiveSelector Hives,
    AiActionFields Fields,
    string? TargetTitle = null);

/// <summary>
/// Which hives the command targets. <see cref="All"/> comes from the literal <c>"all"</c> the model
/// emits for "sve košnice"; otherwise <see cref="Numbers"/> holds the numbers as spoken ("2", "01").
/// Both empty means the command named no hive.
/// </summary>
public sealed record AiHiveSelector(bool All, IReadOnlyList<string> Numbers)
{
    public static readonly AiHiveSelector None = new(false, []);

    public bool IsEmpty => !All && Numbers.Count == 0;
}

/// <summary>
/// The union of fields the v1 actions can carry. Everything is nullable: the model fills only what
/// the beekeeper actually said, and the executor supplies the defaults.
/// </summary>
public sealed record AiActionFields(
    DateOnly? Date = null,
    HoneyLevel? HoneyLevel = null,
    string? BroodStatus = null,
    string? Notes = null,
    string? Title = null,
    TodoPriority? Priority = null,
    DateOnly? DueDate = null,

    // ── MergeBeehive (SPEC-19 §8) ──
    /// <summary>The receiving hive as the beekeeper said it ("3", "01") — resolved like any other hive.</summary>
    string? TargetHive = null,
    int? TargetBeehiveId = null,
    string? TargetBeehiveName = null,
    /// <summary>
    /// Never guessed. When the beekeeper did not say which queen stays, the model leaves this null
    /// and the assistant asks — it is the one irreversible choice in the whole action (SPEC-19 §8).
    /// </summary>
    MergeQueenOutcome? QueenOutcome = null,
    /// <summary>Optional; the executor falls back to <c>Other</c>.</summary>
    MergeReason? MergeReason = null);
