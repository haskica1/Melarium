using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Queens.DTOs;

public class UpdateQueenDto
{
    public int Year { get; set; }
    public QueenMarkColor MarkColor { get; set; }
    public bool IsMarked { get; set; }
    public bool IsClipped { get; set; }
    public QueenOrigin Origin { get; set; }
    public QueenStatus Status { get; set; }
    public DateTime IntroducedDate { get; set; }

    /// <summary>Required when <see cref="Status"/> is not Active; defaults to now if omitted.</summary>
    public DateTime? EndDate { get; set; }

    public string? Notes { get; set; }
}
