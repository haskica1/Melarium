using AutoMapper;
using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Notifications;
using Melarium.Application.Features.Todos.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Todos;

public class TodoService : ITodoService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly INotificationService _notifications;
    private readonly ICurrentUser _currentUser;
    private readonly IAccessGuard _access;

    public TodoService(
        IUnitOfWork uow,
        IMapper mapper,
        INotificationService notifications,
        ICurrentUser currentUser,
        IAccessGuard access)
    {
        _uow           = uow;
        _mapper        = mapper;
        _notifications = notifications;
        _currentUser   = currentUser;
        _access        = access;
    }

    public async Task<IEnumerable<TodoDto>> GetByApiaryIdAsync(int apiaryId)
    {
        if (!await _uow.Apiaries.ExistsAsync(apiaryId))
            throw new NotFoundException(nameof(Apiary), apiaryId);

        await EnsureCanViewApiaryAsync(apiaryId);

        var todos = await _uow.Todos.GetByApiaryIdAsync(apiaryId);
        return _mapper.Map<IEnumerable<TodoDto>>(todos);
    }

    public async Task<IEnumerable<TodoDto>> GetByBeehiveIdAsync(int beehiveId)
    {
        if (!await _uow.Beehives.ExistsAsync(beehiveId))
            throw new NotFoundException(nameof(Beehive), beehiveId);

        await _access.EnsureCanAccessBeehiveAsync(beehiveId);

        var todos = await _uow.Todos.GetByBeehiveIdAsync(beehiveId);
        return _mapper.Map<IEnumerable<TodoDto>>(todos);
    }

    public async Task<TodoDto> GetByIdAsync(int id)
    {
        var todo = await _uow.Todos.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Todo), id);

        await EnsureCanAccessTodoAsync(todo);

        return _mapper.Map<TodoDto>(todo);
    }

    public async Task<TodoDto> CreateAsync(CreateTodoDto dto)
    {
        if (dto.ApiaryId.HasValue)
        {
            if (!await _uow.Apiaries.ExistsAsync(dto.ApiaryId.Value))
                throw new NotFoundException(nameof(Apiary), dto.ApiaryId.Value);
            // Creating an apiary-level todo is a management action.
            await _access.EnsureCanManageApiaryAsync(dto.ApiaryId.Value);
        }

        if (dto.BeehiveId.HasValue)
        {
            if (!await _uow.Beehives.ExistsAsync(dto.BeehiveId.Value))
                throw new NotFoundException(nameof(Beehive), dto.BeehiveId.Value);
            await _access.EnsureCanAccessBeehiveAsync(dto.BeehiveId.Value);
        }

        var todo = _mapper.Map<Todo>(dto);
        todo.CreatedById = _currentUser.UserId;

        await _uow.Todos.AddAsync(todo);
        await _uow.SaveChangesAsync();

        var created = await _uow.Todos.GetByIdWithUsersAsync(todo.Id);

        if (_currentUser.UserId is int creatorId)
        {
            var creator = await _uow.Users.GetByIdWithOrganizationAsync(creatorId);
            if (creator != null)
                await SendTodoCreatedNotificationsAsync(created!, creator);
        }

        return _mapper.Map<TodoDto>(created!);
    }

    public async Task<TodoDto> UpdateAsync(int id, UpdateTodoDto dto)
    {
        var todo = await _uow.Todos.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Todo), id);

        await EnsureCanManageTodoAsync(todo);

        _mapper.Map(dto, todo);

        if (todo.IsCompleted && todo.CompletedAt is null)
            todo.CompletedAt = DateTime.UtcNow;
        else if (!todo.IsCompleted)
            todo.CompletedAt = null;

        todo.UpdatedAt = DateTime.UtcNow;

        await _uow.Todos.UpdateAsync(todo);
        await _uow.SaveChangesAsync();

        var updated = await _uow.Todos.GetByIdWithUsersAsync(id);
        return _mapper.Map<TodoDto>(updated!);
    }

    public async Task DeleteAsync(int id)
    {
        var todo = await _uow.Todos.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Todo), id);

        await EnsureCanManageTodoAsync(todo);

        await _uow.Todos.DeleteAsync(todo);
        await _uow.SaveChangesAsync();
    }

    public async Task<IEnumerable<AssignableUserDto>> GetAssignableUsersForBeehiveAsync(int beehiveId)
    {
        var beehive = await _uow.Beehives.GetByIdAsync(beehiveId)
            ?? throw new NotFoundException(nameof(Beehive), beehiveId);

        await _access.EnsureCanAccessBeehiveAsync(beehiveId);

        var results = new HashSet<User>();

        // ApiaryAdmins responsible for the beehive's apiary…
        var admins = await _uow.Users.FindAsync(u =>
            u.ApiaryId == beehive.ApiaryId && u.Role == UserRole.ApiaryAdmin);
        foreach (var admin in admins)
            results.Add(admin);

        // …plus the Beekeepers assigned to the beehive.
        var beehiveUsers = await _uow.Users.FindAsync(u =>
            u.AssignedBeehives.Any(ub => ub.BeehiveId == beehiveId));
        foreach (var user in beehiveUsers)
            results.Add(user);

        return results
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Select(u => new AssignableUserDto(u.Id, $"{u.FirstName} {u.LastName}"));
    }

    public async Task<IEnumerable<TodoDto>> GetAllOpenForCurrentUserAsync()
    {
        IEnumerable<Todo> todos;

        if (_currentUser.Role == UserRole.SystemAdmin)
        {
            todos = (await _uow.Todos.GetAllAsync()).Where(t => !t.IsCompleted);
        }
        else if (_currentUser.Role == UserRole.Beekeeper)
        {
            var assignedIds = await _access.GetAssignedBeehiveIdsAsync();
            todos = assignedIds.Count > 0
                ? await _uow.Todos.FindAsync(t => !t.IsCompleted && t.BeehiveId.HasValue && assignedIds.Contains(t.BeehiveId.Value))
                : [];
        }
        else if (_currentUser.Role == UserRole.ApiaryAdmin && _currentUser.ApiaryId.HasValue)
        {
            todos = await _uow.Todos.GetAllOpenByApiaryAsync(_currentUser.ApiaryId.Value);
        }
        else if (_currentUser.OrganizationId.HasValue)
        {
            todos = await _uow.Todos.GetAllOpenByOrganizationAsync(_currentUser.OrganizationId.Value);
        }
        else
        {
            todos = [];
        }

        return _mapper.Map<IEnumerable<TodoDto>>(todos);
    }

    // ── Authorization helpers ─────────────────────────────────────────────────

    /// <summary>View access to an apiary: managers within scope, or a Beekeeper assigned to it.</summary>
    private async Task EnsureCanViewApiaryAsync(int apiaryId)
    {
        if (_currentUser.Role == UserRole.Beekeeper)
        {
            var assigned = await _access.GetAssignedApiaryIdsAsync();
            if (!assigned.Contains(apiaryId))
                throw new ForbiddenAccessException();
            return;
        }

        await _access.EnsureCanManageApiaryAsync(apiaryId);
    }

    /// <summary>View access to a todo, based on whether it targets an apiary or a beehive.</summary>
    private async Task EnsureCanAccessTodoAsync(Todo todo)
    {
        if (todo.ApiaryId.HasValue)
            await EnsureCanViewApiaryAsync(todo.ApiaryId.Value);
        else if (todo.BeehiveId.HasValue)
            await _access.EnsureCanAccessBeehiveAsync(todo.BeehiveId.Value);
    }

    /// <summary>Manage access to a todo: apiary todos require management; hive todos follow hive access.</summary>
    private async Task EnsureCanManageTodoAsync(Todo todo)
    {
        if (todo.ApiaryId.HasValue)
            await _access.EnsureCanManageApiaryAsync(todo.ApiaryId.Value);
        else if (todo.BeehiveId.HasValue)
            await _access.EnsureCanAccessBeehiveAsync(todo.BeehiveId.Value);
    }

    // ── Notification helpers ──────────────────────────────────────────────────

    private async Task SendTodoCreatedNotificationsAsync(Todo todo, User creator)
    {
        // Resolve the apiary for context label
        int? apiaryId = todo.ApiaryId;
        if (!apiaryId.HasValue && todo.BeehiveId.HasValue)
        {
            var beehive = await _uow.Beehives.GetByIdAsync(todo.BeehiveId.Value);
            apiaryId = beehive?.ApiaryId;
        }

        var apiary = apiaryId.HasValue ? await _uow.Apiaries.GetByIdAsync(apiaryId.Value) : null;
        var context = apiary != null ? $" u pčelinjaku '{apiary.Name}'" : string.Empty;

        // Notify creator's superior (same cascading rule as beehive creation)
        if (creator.Role == UserRole.ApiaryAdmin)
        {
            var orgAdmins = await _uow.Users.FindAsync(u =>
                u.OrganizationId == creator.OrganizationId && u.Role == UserRole.OrganizationAdmin);

            foreach (var orgAdmin in orgAdmins)
            {
                if (orgAdmin.Id == creator.Id) continue;
                await _notifications.NotifyAsync(
                    orgAdmin.Id,
                    "Novi zadatak",
                    $"Admin {creator.FirstName} {creator.LastName} je kreirao/la zadatak '{todo.Title}'{context}.",
                    NotificationType.TodoCreated,
                    todo.Id, nameof(Todo));
            }
        }
        else if (creator.Role == UserRole.OrganizationAdmin && apiaryId.HasValue)
        {
            var admins = await _uow.Users.FindAsync(u =>
                u.ApiaryId == apiaryId.Value && u.Role == UserRole.ApiaryAdmin);

            foreach (var admin in admins)
            {
                if (admin.Id == creator.Id) continue;
                await _notifications.NotifyAsync(
                    admin.Id,
                    "Novi zadatak",
                    $"Administrator organizacije {creator.FirstName} {creator.LastName} je kreirao/la zadatak '{todo.Title}'{context}.",
                    NotificationType.TodoCreated,
                    todo.Id, nameof(Todo));
            }
        }

        // Notify assignee if different from creator
        if (todo.AssignedToId.HasValue && todo.AssignedToId != creator.Id)
        {
            await _notifications.NotifyAsync(
                todo.AssignedToId.Value,
                "Zadatak vam je dodijeljen",
                $"Zadatak '{todo.Title}' vam je dodijeljen{context}.",
                NotificationType.TodoCreated,
                todo.Id, nameof(Todo));
        }
    }
}
