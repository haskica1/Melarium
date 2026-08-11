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

        // Loaded by apiary, then narrowed to programmes that actually cover one of the caller's own
        // hives. The narrowing is not cosmetic: a Beekeeper's ApiaryIds are the apiaries *containing*
        // their hives, so without it a programme covering only their colleagues' hives would show up
        // on their calendar — and, through the ICS feed, in their personal Google/Apple calendar.
        // For managers it is a no-op, since their BeehiveIds already cover the whole apiary.
        var diets = accessibleApiaryIds.Count > 0 && accessibleBeehiveIds.Count > 0
            ? (await _uow.Diets.GetByApiaryIdsAsync(accessibleApiaryIds))
                .Where(d => d.Beehives.Any(db => db.RemovedOn == null && accessibleBeehiveIds.Contains(db.BeehiveId)))
                .ToList()
            : new List<Melarium.Domain.Entities.Diet>();

        // ── Step 3b: Load treatments with rounds ──────────────────────────────────
        // Same narrowing reasoning as diets above: a colleague's treatment on other hives of the
        // apiary must not land on a Beekeeper's personal calendar.

        var treatments = accessibleApiaryIds.Count > 0 && accessibleBeehiveIds.Count > 0
            ? (await _uow.Treatments.GetByApiaryIdsAsync(accessibleApiaryIds))
                .Where(t => t.Entries.Any(e => accessibleBeehiveIds.Contains(e.BeehiveId)))
                .ToList()
            : new List<Melarium.Domain.Entities.Treatment>();

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
                ApiaryId      = d.ApiaryId,
                ApiaryName    = apiaryNames.TryGetValue(d.ApiaryId, out var aName) ? aName : $"Pčelinjak {d.ApiaryId}",
                HiveCount     = d.Beehives.Count(db => db.RemovedOn == null),
                FoodTypeName  = d.FoodType == FoodType.Custom
                    ? (d.CustomFoodType ?? "Vlastito")
                    : BsLabels.Label(d.FoodType),
            }))
            .ToList();

        var calendarRounds = treatments
            .SelectMany(t => t.Rounds.Select(r => new CalendarTreatmentRoundDto
            {
                Id            = r.Id,
                ScheduledDate = r.ScheduledDate,
                Status        = (int)r.Status,
                StatusName    = BsLabels.Label(r.Status),
                TreatmentId   = t.Id,
                ProductName   = t.ProductName,
                ApiaryId      = t.ApiaryId,
                ApiaryName    = apiaryNames.TryGetValue(t.ApiaryId, out var tName) ? tName : $"Pčelinjak {t.ApiaryId}",
                HiveCount     = t.Entries.Count,
                PurposeName   = BsLabels.Label(t.Purpose),
            }))
            .ToList();

        return new CalendarEventsDto
        {
            Todos           = calendarTodos,
            FeedingEntries  = calendarEntries,
            TreatmentRounds = calendarRounds,
        };
    }
}
