using Melarium.Domain.Common;

namespace Melarium.Domain.Entities;

/// <summary>
/// Represents an apiary (pčelinjak) — a location containing multiple beehives.
/// </summary>
public class Apiary : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>WGS-84 latitude (-90 to 90). Null when no location has been set.</summary>
    public double? Latitude { get; set; }

    /// <summary>WGS-84 longitude (-180 to 180). Null when no location has been set.</summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// The apiary's original ("matična") location, captured once while <see cref="CurrentPastureId"/>
    /// is null and never overwritten by a move. Lets a moved apiary return home reliably. Null when
    /// never captured (apiary predates this field and already moved before it was backfilled).
    /// </summary>
    public double? HomeLatitude { get; set; }
    public double? HomeLongitude { get; set; }

    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    /// <summary>Current pasture (SPEC-10); null = still on its original ("matična") location.</summary>
    public int? CurrentPastureId { get; set; }
    public Pasture? CurrentPasture { get; set; }

    public int? CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public ICollection<Beehive> Beehives { get; set; } = new List<Beehive>();
}
