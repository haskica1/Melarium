using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Todos.DTOs;

public class CreateTodoDto
{
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? DueDate { get; set; }
    public TodoPriority Priority { get; set; } = TodoPriority.Medium;
    public int? AssignedToId { get; set; }

    /// <summary>Set when creating a todo for an apiary.</summary>
    public int? ApiaryId { get; set; }

    /// <summary>Set when creating a todo for a beehive.</summary>
    public int? BeehiveId { get; set; }
}
