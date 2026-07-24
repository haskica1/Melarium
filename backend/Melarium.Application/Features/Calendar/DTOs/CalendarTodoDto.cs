namespace Melarium.Application.Features.Calendar.DTOs;

public class CalendarTodoDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? DueDate { get; set; }
    public int Priority { get; set; }
    public string PriorityName { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int? ApiaryId { get; set; }
    public string? ApiaryName { get; set; }
    public int? BeehiveId { get; set; }
    public string? BeehiveName { get; set; }
}
