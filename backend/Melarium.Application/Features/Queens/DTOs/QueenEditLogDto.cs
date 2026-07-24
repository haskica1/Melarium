namespace Melarium.Application.Features.Queens.DTOs;

/// <summary>One field-level change made to a queen record — read DTO for the edit history view.</summary>
public class QueenEditLogDto
{
    public int Id { get; set; }
    public string FieldLabel { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime EditedAt { get; set; }
    public string? EditedByName { get; set; }
}
