using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

/// <summary>
/// Represents a single beehive (košnica) within an apiary.
/// </summary>
public class Beehive : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public BeehiveType Type { get; set; }
    public BeehiveMaterial Material { get; set; }
    public DateTime DateCreated { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Optional physical label/number painted on the hive (e.g. "1", "01", "A3"). Used by the
    /// "scan by number" flow to resolve a photographed number to this hive; when empty the flow
    /// falls back to a number parsed from <see cref="Name"/>.
    /// </summary>
    public string? LabelNumber { get; set; }

    /// <summary>Permanent, globally-unique identifier for this hive. Encoded into the QR code.</summary>
    public Guid? UniqueId { get; set; }

    /// <summary>PNG QR code image stored as a Base64 string. Generated once on creation.</summary>
    public string? QrCodeBase64 { get; set; }

    public int? CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    // Foreign key
    public int ApiaryId { get; set; }

    // Navigation properties
    public Apiary Apiary { get; set; } = null!;
    public ICollection<Inspection> Inspections { get; set; } = new List<Inspection>();
    public ICollection<Diet> Diets { get; set; } = new List<Diet>();
    public ICollection<UserBeehive> AssignedUsers { get; set; } = new List<UserBeehive>();
    public ICollection<Queen> Queens { get; set; } = new List<Queen>();
}
