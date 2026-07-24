using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

/// <summary>
/// Represents a queen bee (matica) associated with a beehive over a period of time.
/// A beehive has at most one <see cref="QueenStatus.Active"/> queen; older rows form
/// the replacement history.
/// </summary>
public class Queen : BaseEntity
{
    /// <summary>Year of birth — determines the international marking color.</summary>
    public int Year { get; set; }

    public QueenMarkColor MarkColor { get; set; }

    /// <summary>Whether the queen is physically marked.</summary>
    public bool IsMarked { get; set; }

    /// <summary>Whether one of the queen's wings is clipped.</summary>
    public bool IsClipped { get; set; }

    public QueenOrigin Origin { get; set; }

    public QueenStatus Status { get; set; }

    public DateTime IntroducedDate { get; set; }

    /// <summary>Set when the queen stops being active (replaced, died or missing).</summary>
    public DateTime? EndDate { get; set; }

    public string? Notes { get; set; }

    // Foreign key
    public int BeehiveId { get; set; }

    // Navigation property
    public Beehive Beehive { get; set; } = null!;
}
