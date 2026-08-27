using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Calendar;

/// <summary>
/// Role-based access resolution for calendar features. Mirrors the tenancy rules used elsewhere:
/// SystemAdmin sees everything, OrganizationAdmin its organization, ApiaryAdmin its apiary, and a
/// Beekeeper only their assigned hives. Extracted from <see cref="CalendarService"/> so the feed and
/// the daily agenda reuse the exact same authorization.
/// </summary>
public class CalendarAccessResolver : ICalendarAccessResolver
{
    private readonly IUnitOfWork _uow;

    public CalendarAccessResolver(IUnitOfWork uow) => _uow = uow;

    public async Task<CalendarScope> ResolveAsync(CalendarUserContext ctx)
    {
        if (ctx.Role == UserRole.SystemAdmin)
        {
            var allBeehives = (await _uow.Beehives.GetAllActiveAsync()).ToList();
            var allApiaries = (await _uow.Apiaries.GetAllAsync()).ToList();
            return new CalendarScope
            {
                BeehiveIds   = allBeehives.Select(b => b.Id).ToHashSet(),
                ApiaryIds    = allApiaries.Select(a => a.Id).ToHashSet(),
                BeehiveNames = allBeehives.ToDictionary(b => b.Id, b => b.Name),
                ApiaryNames  = allApiaries.ToDictionary(a => a.Id, a => a.Name),
            };
        }

        if (ctx.Role == UserRole.OrganizationAdmin && ctx.OrganizationId is int orgId)
        {
            var beehives = (await _uow.Beehives.GetByOrganizationAsync(orgId)).ToList();
            var apiaries = (await _uow.Apiaries.GetAllByOrganizationAsync(orgId)).ToList();
            return new CalendarScope
            {
                BeehiveIds   = beehives.Select(b => b.Id).ToHashSet(),
                ApiaryIds    = apiaries.Select(a => a.Id).ToHashSet(),
                BeehiveNames = beehives.ToDictionary(b => b.Id, b => b.Name),
                ApiaryNames  = apiaries.ToDictionary(a => a.Id, a => a.Name),
            };
        }

        if (ctx.Role == UserRole.ApiaryAdmin && ctx.ApiaryId is int apiaryId)
        {
            var beehives = (await _uow.Beehives.GetByApiaryIdAsync(apiaryId)).ToList();
            var apiary   = await _uow.Apiaries.GetByIdAsync(apiaryId);
            return new CalendarScope
            {
                BeehiveIds   = beehives.Select(b => b.Id).ToHashSet(),
                ApiaryIds    = new HashSet<int> { apiaryId },
                BeehiveNames = beehives.ToDictionary(b => b.Id, b => b.Name),
                ApiaryNames  = apiary != null
                    ? new Dictionary<int, string> { { apiary.Id, apiary.Name } }
                    : new Dictionary<int, string>(),
            };
        }

        if (ctx.Role == UserRole.Beekeeper)
        {
            var assignedIds = await _uow.Users.GetAssignedBeehiveIdsAsync(ctx.UserId);
            var beehives = assignedIds.Count > 0
                ? (await _uow.Beehives.FindAsync(b =>
                    assignedIds.Contains(b.Id) && b.MergedIntoBeehiveId == null)).ToList()
                : new List<Melarium.Domain.Entities.Beehive>();

            var apiaryIds = beehives.Select(b => b.ApiaryId).ToHashSet();
            var apiaries = apiaryIds.Count > 0
                ? (await _uow.Apiaries.FindAsync(a => apiaryIds.Contains(a.Id))).ToList()
                : new List<Melarium.Domain.Entities.Apiary>();

            return new CalendarScope
            {
                BeehiveIds   = assignedIds,
                ApiaryIds    = apiaryIds,
                BeehiveNames = beehives.ToDictionary(b => b.Id, b => b.Name),
                ApiaryNames  = apiaries.ToDictionary(a => a.Id, a => a.Name),
            };
        }

        return new CalendarScope();
    }
}
