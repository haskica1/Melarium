using Melarium.Domain.Common;

namespace Melarium.Domain.Entities;

/// <summary>
/// An org-scoped named pasture location (pašnjak) for migratory beekeeping (SPEC-10). Exists
/// independently of apiaries and is reused season after season; multiple apiaries may sit on the
/// same pasture at once.
/// </summary>
public class Pasture : BaseEntity
{
    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>WGS-84; null = pasture recorded without a map location.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string? Address { get; set; }

    /// <summary>What blooms there and when — "bagrem, lipa; paša traje V–VI".</summary>
    public string? FloraNotes { get; set; }

    public string? Notes { get; set; }
}
