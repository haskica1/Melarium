namespace Melarium.Domain.Enums;

/// <summary>
/// What an AI-assistant action does when confirmed (SPEC-17). Every kind maps to an existing
/// application service call — the assistant never reaches a repository directly.
/// </summary>
public enum AiActionKind
{
    CreateInspection = 1,
    CreateTodo       = 2,

    // ── Phase C — mutate or remove an existing record. Each requires a second, separate
    // confirmation on top of the first (SPEC-17 D5, §7.3) because it can overwrite or destroy
    // data a create action never could. ──
    UpdateTodo       = 3,
    CompleteTodo     = 4,
    UpdateInspection = 5,
    DeleteTodo       = 6,
    DeleteInspection = 7,

    // ── SPEC-19 D5 — sastavljanje društava. Destructive: it takes a hive out of the apiary for
    // good, so it requires the second confirmation like the kinds above. ──
    MergeBeehive     = 8,
}
