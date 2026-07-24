using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Queens.DTOs;

/// <summary>
/// Registers a new (always Active) queen on a beehive. If the hive already has an active
/// queen, it is automatically closed as Replaced in the same transaction.
/// </summary>
public class CreateQueenDto
{
    public int Year { get; set; }

    /// <summary>Optional — when null, derived from <see cref="Year"/> per the international color code.</summary>
    public QueenMarkColor? MarkColor { get; set; }

    public bool IsMarked { get; set; }
    public bool IsClipped { get; set; }
    public QueenOrigin Origin { get; set; }
    public DateTime IntroducedDate { get; set; }
    public string? Notes { get; set; }
}
