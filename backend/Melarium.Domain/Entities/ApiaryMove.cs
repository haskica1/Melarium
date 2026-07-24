using Melarium.Domain.Common;

namespace Melarium.Domain.Entities;

/// <summary>
/// One relocation event (selidba): the apiary moved <b>to</b> a pasture on a date (SPEC-10).
/// <see cref="FromPastureId"/> is resolved server-side from the apiary's pasture at the moment of
/// the move; null = the move started from the apiary's original ("matična") location.
/// </summary>
public class ApiaryMove : BaseEntity
{
    public int ApiaryId { get; set; }
    public Apiary Apiary { get; set; } = null!;

    public int? FromPastureId { get; set; }
    public Pasture? FromPasture { get; set; }

    /// <summary>Null = moved back to the apiary's original ("matična") location.</summary>
    public int? ToPastureId { get; set; }
    public Pasture? ToPasture { get; set; }

    public DateTime MovedAt { get; set; }

    /// <summary>Broj veterinarske svjedodžbe — legally expected when moving hives.</summary>
    public string? CertificateNumber { get; set; }

    public string? Notes { get; set; }

    public int? CreatedById { get; set; }
    public User? CreatedBy { get; set; }
}
