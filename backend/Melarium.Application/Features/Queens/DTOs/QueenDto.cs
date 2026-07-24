using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Queens.DTOs;

/// <summary>Queen data transfer object used for reads.</summary>
public class QueenDto
{
    public int Id { get; set; }
    public int Year { get; set; }
    public QueenMarkColor MarkColor { get; set; }
    public string MarkColorName { get; set; } = string.Empty;
    public bool IsMarked { get; set; }
    public bool IsClipped { get; set; }
    public QueenOrigin Origin { get; set; }
    public string OriginName { get; set; } = string.Empty;
    public QueenStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public DateTime IntroducedDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Notes { get; set; }
    public int BeehiveId { get; set; }
    public DateTime CreatedAt { get; set; }
}
