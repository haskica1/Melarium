using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Localization;
using Melarium.Application.Features.Stats.DTOs;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Stats;

public class StatsService : IStatsService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly Common.Security.IPlanLock _planLock;

    public StatsService(IUnitOfWork uow, ICurrentUser currentUser, Common.Security.IPlanLock planLock)
    {
        _uow = uow;
        _currentUser = currentUser;
        _planLock = planLock;
    }

    public async Task<StatsDto> GetStatsAsync()
    {
        // SystemAdmin sees platform-wide stats; everyone else is scoped to their organization.
        int? organizationId = _currentUser.Role == UserRole.SystemAdmin
            ? null
            : _currentUser.OrganizationId;

        // ── Fetch base data ────────────────────────────────────────────────────

        // Rows the plan locked away are dropped before anything is derived from them (SPEC-24) —
        // every count, average and chart below reads off these two lists, so filtering here is what
        // keeps the dashboard from reporting on 50 hives while only 7 of them can be opened.
        var locked = organizationId.HasValue
            ? await _planLock.GetForOrganizationAsync(organizationId.Value)
            : PlanLockResult.Empty;

        var apiaries = (organizationId.HasValue
                ? (await _uow.Apiaries.GetAllByOrganizationAsync(organizationId.Value))
                : (await _uow.Apiaries.GetAllAsync()))
            .Where(a => !locked.ApiaryIds.Contains(a.Id))
            .ToList();

        var apiaryIds   = apiaries.Select(a => a.Id).ToHashSet();
        var apiaryNames = apiaries.ToDictionary(a => a.Id, a => a.Name);

        var beehives = (organizationId.HasValue
                ? (await _uow.Beehives.GetByOrganizationAsync(organizationId.Value))
                : (await _uow.Beehives.GetAllActiveAsync()))
            .Where(b => !locked.BeehiveIds.Contains(b.Id))
            .ToList();

        var beehiveIds = beehives.Select(b => b.Id).ToHashSet();
        var beehiveNamesById = beehives.ToDictionary(b => b.Id, b => b.Name);

        var inspections = beehiveIds.Count > 0
            ? (await _uow.Inspections.FindAsync(i => beehiveIds.Contains(i.BeehiveId))).ToList()
            : [];

        // By apiary, not by a join through the hive links: a programme whose hives were all removed
        // is still a live programme and still listed on /feedings, so it must still be counted here.
        var diets = apiaryIds.Count > 0
            ? (await _uow.Diets.GetByApiaryIdsAsync(apiaryIds)).ToList()
            : [];

        var todos = beehiveIds.Count > 0 || apiaryIds.Count > 0
            ? (await _uow.Todos.FindAsync(t =>
                (t.ApiaryId.HasValue  && apiaryIds.Contains(t.ApiaryId.Value)) ||
                (t.BeehiveId.HasValue && beehiveIds.Contains(t.BeehiveId.Value))
              )).ToList()
            : [];

        // ── Summary ────────────────────────────────────────────────────────────

        var activeDiets   = diets.Count(d => d.Status == DietStatus.InProgress || d.Status == DietStatus.NotStarted);
        var pendingTodos  = todos.Count(t => !t.IsCompleted);

        // ── Beehive distributions ──────────────────────────────────────────────

        var byType = beehives
            .GroupBy(b => b.Type)
            .Select(g => new NameValueDto(BsLabels.Label(g.Key), g.Count()))
            .OrderByDescending(x => x.Value)
            .ToList();

        var byMaterial = beehives
            .GroupBy(b => b.Material)
            .Select(g => new NameValueDto(BsLabels.Label(g.Key), g.Count()))
            .OrderByDescending(x => x.Value)
            .ToList();

        // ── Honey level distribution ───────────────────────────────────────────

        var honeyDist = inspections
            .GroupBy(i => i.HoneyLevel)
            .Select(g => new NameValueDto(BsLabels.Label(g.Key), g.Count()))
            .OrderBy(x => x.Name)
            .ToList();

        // ── Inspections per month (last 12 months) ─────────────────────────────

        var last12 = GenerateLast12Months();
        var inspByMonth = inspections
            .Where(i => i.Date >= DateTime.UtcNow.AddMonths(-12))
            .GroupBy(i => new { i.Date.Year, i.Date.Month })
            .ToDictionary(g => (g.Key.Year, g.Key.Month), g => g.Count());

        var inspectionsByMonth = last12
            .Select(m => new MonthCountDto(
                m.Label,
                inspByMonth.TryGetValue((m.Year, m.Month), out var c) ? c : 0))
            .ToList();

        // ── Temperature by month (last 12 months) ──────────────────────────────

        var tempByMonth = inspections
            .Where(i => i.Date >= DateTime.UtcNow.AddMonths(-12) && i.Temperature.HasValue)
            .GroupBy(i => new { i.Date.Year, i.Date.Month })
            .ToDictionary(
                g => (g.Key.Year, g.Key.Month),
                g => (
                    Avg: Math.Round(g.Average(i => i.Temperature!.Value), 1),
                    Min: Math.Round(g.Min(i => i.Temperature!.Value), 1),
                    Max: Math.Round(g.Max(i => i.Temperature!.Value), 1)
                )
            );

        var temperatureByMonth = last12
            .Select(m => tempByMonth.TryGetValue((m.Year, m.Month), out var t)
                ? new MonthTempDto(m.Label, t.Avg, t.Min, t.Max)
                : new MonthTempDto(m.Label, null, null, null))
            .ToList();

        // ── Diet distributions ─────────────────────────────────────────────────

        var dietsByStatus = diets
            .GroupBy(d => d.Status)
            .Select(g => new NameValueDto(BsLabels.Label(g.Key), g.Count()))
            .OrderByDescending(x => x.Value)
            .ToList();

        var dietsByFoodType = diets
            .GroupBy(d => d.FoodType)
            .Select(g => new NameValueDto(BsLabels.Label(g.Key), g.Count()))
            .OrderByDescending(x => x.Value)
            .ToList();

        // ── Top beehives by inspection count ──────────────────────────────────

        var topBeehives = beehives
            .Select(b => new NameValueDto(
                b.Name,
                inspections.Count(i => i.BeehiveId == b.Id)))
            .OrderByDescending(x => x.Value)
            .Take(8)
            .ToList();

        // ── Apiaries by beehive count ──────────────────────────────────────────

        var apiariesByCount = beehives
            .GroupBy(b => b.ApiaryId)
            .Select(g => new NameValueDto(
                apiaryNames.TryGetValue(g.Key, out var name) ? name : $"Apiary {g.Key}",
                g.Count()))
            .OrderByDescending(x => x.Value)
            .ToList();

        // ── Todos by priority ──────────────────────────────────────────────────

        var todosByPriority = todos
            .GroupBy(t => t.Priority)
            .Select(g => new PriorityStatsDto(
                BsLabels.Label(g.Key),
                g.Count(),
                g.Count(t => t.IsCompleted)))
            .OrderByDescending(x => x.Total)
            .ToList();

        // ── Harvests (SPEC-02) ─────────────────────────────────────────────────

        var currentYear = DateTime.UtcNow.Year;

        var harvests = apiaryIds.Count > 0
            ? (await _uow.Harvests.GetByApiariesAsync(apiaryIds)).ToList()
            : [];

        var currentYearHarvests = harvests.Where(h => h.Date.Year == currentYear).ToList();

        var seasonTotalKg = currentYearHarvests.Sum(h => h.Entries.Sum(e => e.QuantityKg));

        var estimatedRevenue = currentYearHarvests
            .Where(h => h.PricePerKg.HasValue)
            .Sum(h => h.Entries.Sum(e => e.QuantityKg) * h.PricePerKg!.Value);

        var kgByApiary = currentYearHarvests
            .GroupBy(h => h.ApiaryId)
            .Select(g => new NameDecimalDto(
                apiaryNames.TryGetValue(g.Key, out var name) ? name : $"Pčelinjak {g.Key}",
                g.Sum(h => h.Entries.Sum(e => e.QuantityKg))))
            .OrderByDescending(x => x.Value)
            .ToList();

        var kgByHoneyType = currentYearHarvests
            .GroupBy(h => h.HoneyType)
            .Select(g => new NameDecimalDto(
                BsLabels.Label(g.Key),
                g.Sum(h => h.Entries.Sum(e => e.QuantityKg))))
            .OrderByDescending(x => x.Value)
            .ToList();

        var topHivesByYield = currentYearHarvests
            .SelectMany(h => h.Entries)
            .GroupBy(e => e.BeehiveId)
            .Select(g => new NameDecimalDto(
                beehiveNamesById.TryGetValue(g.Key, out var name) ? name : $"Košnica {g.Key}",
                g.Sum(e => e.QuantityKg)))
            .OrderByDescending(x => x.Value)
            .Take(5)
            .ToList();

        var yearlyYield = Enumerable.Range(0, 3)
            .Select(offset => currentYear - 2 + offset)
            .Select(y => new NameDecimalDto(
                y.ToString(),
                harvests.Where(h => h.Date.Year == y).Sum(h => h.Entries.Sum(e => e.QuantityKg))))
            .ToList();

        // ── Feeding cost (SPEC-12 Phase E) ─────────────────────────────────────
        // Symmetric with SeasonTotalKg/EstimatedRevenue above: current year, one query.

        var currentYearDietIds = diets.Where(d => d.StartDate.Year == currentYear).Select(d => d.Id).ToList();
        var dietCostTotals = currentYearDietIds.Count > 0
            ? await _uow.Expenses.GetTotalsByDietsAsync(currentYearDietIds)
            : [];
        var feedingCost = dietCostTotals.Values
            .SelectMany(totals => totals)
            .Where(t => t.Currency == "BAM")
            .Sum(t => t.Total);

        // ── Yield per pasture (SPEC-10) ────────────────────────────────────────

        var moves = apiaryIds.Count > 0
            ? (await _uow.ApiaryMoves.GetByApiariesAsync(apiaryIds)).ToList()
            : [];

        List<NameDecimalDto> kgByPasture = [];
        if (moves.Count > 0)
        {
            var movesByApiary = moves.GroupBy(m => m.ApiaryId).ToDictionary(g => g.Key, g => g.ToList());
            var pastureNames = moves
                .Select(m => m.ToPasture)
                .Where(p => p is not null)
                .DistinctBy(p => p!.Id)
                .ToDictionary(p => p!.Id, p => p!.Name);

            kgByPasture = currentYearHarvests
                .GroupBy(h => PastureAttribution.ResolveToPastureId(
                    movesByApiary.TryGetValue(h.ApiaryId, out var apiaryMoves) ? apiaryMoves : (List<ApiaryMove>)[],
                    h.Date))
                .Select(g => new NameDecimalDto(
                    g.Key is int pastureId
                        ? pastureNames.TryGetValue(pastureId, out var name) ? name : $"Pašnjak {pastureId}"
                        : "Matična lokacija",
                    g.Sum(h => h.Entries.Sum(e => e.QuantityKg))))
                .OrderByDescending(x => x.Value)
                .ToList();
        }

        // ── Build result ───────────────────────────────────────────────────────

        return new StatsDto
        {
            TotalApiaries         = apiaries.Count,
            TotalBeehives         = beehives.Count,
            TotalInspections      = inspections.Count,
            ActiveDiets           = activeDiets,
            PendingTodos          = pendingTodos,
            BeehivesByType        = byType,
            BeehivesByMaterial    = byMaterial,
            HoneyLevelDistribution= honeyDist,
            InspectionsByMonth    = inspectionsByMonth,
            TemperatureByMonth    = temperatureByMonth,
            DietsByStatus         = dietsByStatus,
            DietsByFoodType       = dietsByFoodType,
            TopBeehivesByInspections = topBeehives,
            ApiariesByBeehiveCount   = apiariesByCount,
            TodosByPriority          = todosByPriority,
            SeasonTotalKg            = seasonTotalKg,
            EstimatedRevenue         = estimatedRevenue,
            KgByApiary               = kgByApiary,
            KgByHoneyType            = kgByHoneyType,
            TopHivesByYield          = topHivesByYield,
            YearlyYield              = yearlyYield,
            KgByPasture              = kgByPasture,
            FeedingCost              = feedingCost,
        };
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static IReadOnlyList<(int Year, int Month, string Label)> GenerateLast12Months()
    {
        var list = new List<(int, int, string)>();
        var now = DateTime.UtcNow;
        for (int i = 11; i >= 0; i--)
        {
            var d = now.AddMonths(-i);
            list.Add((d.Year, d.Month, BsLabels.MonthShort(d.Year, d.Month)));
        }
        return list;
    }
}
