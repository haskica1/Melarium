using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Localization;
using Melarium.Application.Features.Calendar.DTOs;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Calendar;

public class CalendarService : ICalendarService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly ICalendarAccessResolver _resolver;

    public CalendarService(IUnitOfWork uow, ICurrentUser currentUser, ICalendarAccessResolver resolver)
    {
        _uow = uow;
        _currentUser = currentUser;
        _resolver = resolver;
    }

    public async Task<CalendarEventsDto> GetCalendarEventsAsync()
    {
        var role   = _currentUser.Role;
        var userId = _currentUser.UserId;

        if (role is null || userId is null)
            return new CalendarEventsDto();

        // ── Step 1: Resolve accessible IDs and name lookup dictionaries ───────────
        // Shared with the ICS feed + daily agenda so authorization lives in one place (SPEC-11).

        var scope = await _resolver.ResolveAsync(
            new CalendarUserContext(userId.Value, role.Value, _currentUser.OrganizationId, _currentUser.ApiaryId));

        var accessibleApiaryIds  = scope.ApiaryIds;
        var accessibleBeehiveIds = scope.BeehiveIds;
        var beehiveNames         = scope.BeehiveNames;
        var apiaryNames          = scope.ApiaryNames;

        // ── Step 2: Load todos ────────────────────────────────────────────────────

        List<Melarium.Domain.Entities.Todo> todos;

        if (role == UserRole.Beekeeper && userId.HasValue)
        {
            todos = (await _uow.Todos.FindAsync(t => t.AssignedToId == userId.Value)).ToList();
        }
        else if (accessibleApiaryIds.Count > 0 || accessibleBeehiveIds.Count > 0)
        {
            todos = (await _uow.Todos.FindAsync(t =>
                (t.ApiaryId.HasValue  && accessibleApiaryIds.Contains(t.ApiaryId.Value)) ||
                (t.BeehiveId.HasValue && accessibleBeehiveIds.Contains(t.BeehiveId.Value))
            )).ToList();
        }
        else
        {
            todos = new List<Melarium.Domain.Entities.Todo>();
        }

        // ── Step 3: Load diets with feeding entries ───────────────────────────────

        var diets = accessibleBeehiveIds.Count > 0
            ? (await _uow.Diets.GetByBeehiveIdsAsync(accessibleBeehiveIds)).ToList()
            : new List<Melarium.Domain.Entities.Diet>();

        // ── Step 4: Build response ────────────────────────────────────────────────

        var calendarTodos = todos
            .Where(t => t.DueDate.HasValue)
            .Select(t => new CalendarTodoDto
            {
                Id           = t.Id,
                Title        = t.Title,
                Notes        = t.Notes,
                DueDate      = t.DueDate,
                Priority     = (int)t.Priority,
                PriorityName = BsLabels.Label(t.Priority),
                IsCompleted  = t.IsCompleted,
                ApiaryId     = t.ApiaryId,
                ApiaryName   = t.ApiaryId.HasValue && apiaryNames.TryGetValue(t.ApiaryId.Value, out var an) ? an : null,
                BeehiveId    = t.BeehiveId,
                BeehiveName  = t.BeehiveId.HasValue && beehiveNames.TryGetValue(t.BeehiveId.Value, out var bn) ? bn : null,
            })
            .ToList();

        var calendarEntries = diets
            // A diet stopped early is no longer scheduled — drop it from the calendar entirely
            // (its remaining entries would otherwise linger as never-completing "overdue" feedings).
            .Where(d => d.Status != DietStatus.StoppedEarly)
            .SelectMany(d => d.FeedingEntries.Select(e => new CalendarFeedingEntryDto
            {
                Id            = e.Id,
                ScheduledDate = e.ScheduledDate,
                Status        = (int)e.Status,
                StatusName    = BsLabels.Label(e.Status),
                DietId        = d.Id,
                DietName      = d.Name,
                BeehiveId     = d.BeehiveId,
                BeehiveName   = beehiveNames.TryGetValue(d.BeehiveId, out var bName) ? bName : $"Košnica {d.BeehiveId}",
                FoodTypeName  = d.FoodType == FoodType.Custom
                    ? (d.CustomFoodType ?? "Vlastito")
                    : BsLabels.Label(d.FoodType),
            }))
            .ToList();

        return new CalendarEventsDto
        {
            Todos          = calendarTodos,
            FeedingEntries = calendarEntries,
        };
    }
}
