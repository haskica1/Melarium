using Melarium.Application.Features.Todos.DTOs;

namespace Melarium.Application.Features.Todos;

public interface ITodoService
{
    Task<IEnumerable<TodoDto>> GetByApiaryIdAsync(int apiaryId);
    Task<IEnumerable<TodoDto>> GetByBeehiveIdAsync(int beehiveId);
    Task<TodoDto> GetByIdAsync(int id);
    Task<TodoDto> CreateAsync(CreateTodoDto dto);
    Task<TodoDto> UpdateAsync(int id, UpdateTodoDto dto);
    Task DeleteAsync(int id);
    Task<IEnumerable<AssignableUserDto>> GetAssignableUsersForBeehiveAsync(int beehiveId);

    /// <summary>Returns all open (non-completed) todos accessible to the current user (role-scoped).</summary>
    Task<IEnumerable<TodoDto>> GetAllOpenForCurrentUserAsync();
}
