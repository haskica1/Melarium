namespace Melarium.Domain.Enums;

/// <summary>
/// Lifecycle of a proposed AI-assistant action (SPEC-17). The <see cref="Pending"/> → <see cref="Confirmed"/>
/// transition is also the double-confirm guard: a turn whose actions have already left
/// <see cref="Pending"/> is refused rather than executed again.
/// </summary>
public enum AiActionStatus
{
    /// <summary>Proposed and shown to the user; nothing written yet.</summary>
    Pending   = 1,

    /// <summary>Confirmed and executed successfully.</summary>
    Confirmed = 2,

    /// <summary>The user unchecked it, or rejected the whole proposal.</summary>
    Rejected  = 3,

    /// <summary>Confirmed but the execution failed; <c>ErrorMessage</c> carries the reason.</summary>
    Failed    = 4,
}
