using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Dashboard.DTOs;

public record DashboardTodoDto(
    int Id,
    string Title,
    DateTime? DueDate,
    TodoPriority Priority,
    bool IsOverdue,
    string? ScopeName,
    string LinkPath);
