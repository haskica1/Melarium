using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Beehives.DTOs;

/// <summary>Lightweight beehive representation for list/summary views.</summary>
public class BeehiveDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public BeehiveType Type { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public BeehiveMaterial Material { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; }
    public string? Notes { get; set; }
    public string? LabelNumber { get; set; }
    public int ApiaryId { get; set; }
    public int InspectionCount { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? UniqueId { get; set; }

    /// <summary>
    /// True when the organization's plan no longer reaches this hive (SPEC-24) — either it ranks past
    /// the hive limit, or its whole apiary is locked. Listed but stripped; opening it returns 402.
    /// </summary>
    public bool IsLocked { get; set; }

    // ── Colony merge (SPEC-19). Null on every hive that is still in service, which is every hive
    // a normal list returns — these are populated only in the archive and on the detail DTO. ──
    public int? MergedIntoBeehiveId { get; set; }
    public string? MergedIntoBeehiveName { get; set; }
    public DateTime? MergedAt { get; set; }
}
