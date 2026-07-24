using Melarium.Domain.Enums;

namespace Melarium.Domain.Common;

/// <summary>
/// Lightweight read model: the most recent treatment for one hive. Consumers derive
/// karenca/status via <see cref="TreatmentStatusHelper"/>. Used by the hive badge, the harvest-date
/// warning (SPEC-02), the advisor context (SPEC-01), and alert rules (SPEC-04).
/// </summary>
public record TreatmentLatestInfo(
    int BeehiveId,
    int TreatmentId,
    string ProductName,
    ActiveSubstance ActiveSubstance,
    TreatmentPurpose Purpose,
    ApplicationMethod Method,
    DateTime StartDate,
    DateTime? EndDate,
    int WithdrawalDays);
