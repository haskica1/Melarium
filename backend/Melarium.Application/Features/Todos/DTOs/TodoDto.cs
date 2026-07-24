using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Todos.DTOs;

/// <summary>Read model for a to-do item.</summary>
public class TodoDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? DueDate { get; set; }
    public TodoPriority Priority { get; set; }
    public string PriorityName { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? ApiaryId { get; set; }
    public int? BeehiveId { get; set; }
    public string? CreatedByName { get; set; }
    public int? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime CreatedAt { get; set; }
}
