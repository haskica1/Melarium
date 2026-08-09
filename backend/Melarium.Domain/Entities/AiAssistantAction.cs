using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

/// <summary>
/// One action the assistant proposed for a turn (SPEC-17). Rows are created in
/// <see cref="AiActionStatus.Pending"/> and only leave that state through an explicit human
/// confirmation.
/// </summary>
public class AiAssistantAction : BaseEntity
{
    public int TurnId { get; set; }
    public AiAssistantTurn Turn { get; set; } = null!;

    public AiActionKind Kind { get; set; }

    /// <summary>The resolved DTO as JSON — what would be posted if the user confirms.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>Human-readable target for the history page, e.g. "Zlatna dolina · Košnica 2".</summary>
    public string TargetSummary { get; set; } = string.Empty;

    public AiActionStatus Status { get; set; } = AiActionStatus.Pending;

    /// <summary>Entity type created on success ("Inspection", "Todo") — the history page's link target.</summary>
    public string? ResultEntityType { get; set; }

    /// <summary>
    /// Id of the created record. Deliberately <b>not</b> a foreign key: deleting the inspection later
    /// must not delete the audit row saying the assistant created it.
    /// </summary>
    public int? ResultEntityId { get; set; }

    /// <summary>Bosnian failure reason when <see cref="Status"/> is <see cref="AiActionStatus.Failed"/>.</summary>
    public string? ErrorMessage { get; set; }
}
