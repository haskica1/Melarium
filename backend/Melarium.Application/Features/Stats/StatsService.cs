using Melarium.Application.Common;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Localization;
using Melarium.Application.Features.Stats.DTOs;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Stats;

public class StatsService : IStatsService
{
    /// <summary>The row of records kept for the whole organization rather than one apiary (SPEC-30).</summary>
    private const string SharedLabel = "Zajedničko";

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

        // Honey only — the other products are their own section below and never join these kg
        // (SPEC-30). The organization's own records (no apiary) are honey of the organization too,
        // and a record kept as one figure counts in full everywhere except the per-hive chart.
        var harvests = (apiaryIds.Count > 0
                ? (await _uow.Harvests.GetByApiariesAsync(apiaryIds, HarvestKind.Honey)).ToList()
                : [])
            .Concat(await _uow.Harvests.GetSharedAsync(organizationId, HarvestKind.Honey))
            .ToList();

        var currentYearHarvests = harvests.Where(h => h.Date.Year == currentYear).ToList();

        var seasonTotalKg = currentYearHarvests.Sum(HarvestTotals.TotalKg);

        var estimatedRevenue = currentYearHarvests.Sum(h => HarvestTotals.EstimatedRevenue(h) ?? 0m);

        var kgByApiary = currentYearHarvests
            .GroupBy(h => h.ApiaryId)
            .Select(g => new NameDecimalDto(
                g.Key is int id
                    ? apiaryNames.TryGetValue(id, out var name) ? name : $"Pčelinjak {id}"
                    : SharedLabel,
                g.Sum(HarvestTotals.TotalKg)))
            .OrderByDescending(x => x.Value)
            .ToList();

        var kgByHoneyType = currentYearHarvests
            .GroupBy(h => h.HoneyType)
            .Select(g => new NameDecimalDto(
                g.Key is HoneyType t ? BsLabels.Label(t) : "—",
                g.Sum(HarvestTotals.TotalKg)))
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
                harvests.Where(h => h.Date.Year == y).Sum(HarvestTotals.TotalKg)))
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

        // Shared with the other products below, so a pasture is named and bucketed the same in both.
        var pastures = PastureBuckets.From(apiaryIds.Count > 0
            ? await _uow.ApiaryMoves.GetByApiariesAsync(apiaryIds)
            : []);

        // A record of the whole organization stood on no one pasture — it gets a bucket of its own.
        var kgByPasture = pastures is null
            ? []
            : currentYearHarvests
                .GroupBy(pastures.Of)
                .Select(g => new NameDecimalDto(pastures.NameOf(g.Key), g.Sum(HarvestTotals.TotalKg)))
                .OrderByDescending(x => x.Value)
                .ToList();

        // ── Other hive products (SPEC-30) ──────────────────────────────────────
        // Current year, like the honey figures above. Shared records (no apiary) are the
        // organization's own: they count toward the per-type totals and get a row of their own.

        var productRecords = (apiaryIds.Count > 0
                ? (await _uow.Harvests.GetByApiariesAsync(apiaryIds, HarvestKind.OtherProducts, currentYear)).ToList()
                : [])
            .Concat(await _uow.Harvests.GetSharedAsync(organizationId, HarvestKind.OtherProducts, currentYear))
            .ToList();

        // Every product side by side, honey first by the enum — the tiles that open "Prinosi". Revenue
        // is the one figure a client may add up across them.
        var harvestsByProduct = currentYearHarvests
            .Concat(productRecords)
            .GroupBy(p => p.ProductType)
            .OrderBy(g => g.Key)
            .Select(g => new ProductTypeTotalDto(
                g.Key,
                BsLabels.Label(g.Key),
                g.Sum(HarvestTotals.TotalKg),
                g.Sum(p => HarvestTotals.EstimatedRevenue(p) ?? 0m),
                g.Where(p => p.PricePerKg is null).Sum(HarvestTotals.TotalKg),
                g.Count()))
            .ToList();

        var hiveProductsByApiary = productRecords
            .GroupBy(p => p.ApiaryId)
            .Select(g => new ApiaryProductTotalsDto(
                g.Key,
                g.Key is int id
                    ? apiaryNames.TryGetValue(id, out var name) ? name : $"Pčelinjak {id}"
                    : SharedLabel,
                g.GroupBy(p => p.ProductType)
                 .OrderBy(t => t.Key)
                 .Select(t => new ProductKgDto(t.Key, BsLabels.Label(t.Key), t.Sum(HarvestTotals.TotalKg)))
                 .ToList()))
            .OrderBy(x => x.ApiaryId is null)
            .ThenBy(x => x.ApiaryName)
            .ToList();

        var hiveProductsByPasture = pastures is null
            ? []
            : pastures.Order(productRecords.GroupBy(pastures.Of), g => g.Key)
                .Select(g => new NamedProductTotalsDto(pastures.NameOf(g.Key), ProductItems(g)))
                .ToList();

        // Per-hive lines only — a record kept as one figure has no hive. A hive the plan locked is left
        // out (SPEC-24); by name, since no single kg can rank a row that holds several products.
        var hiveProductsByBeehive = productRecords
            .SelectMany(p => p.Entries.Select(e => (e.BeehiveId, p.ProductType, e.QuantityKg)))
            .Where(x => !locked.BeehiveIds.Contains(x.BeehiveId))
            .GroupBy(x => x.BeehiveId)
            .Select(g => new NamedProductTotalsDto(
                beehiveNamesById.TryGetValue(g.Key, out var name) ? name : $"Košnica {g.Key}",
                g.GroupBy(x => x.ProductType)
                 .OrderBy(t => t.Key)
                 .Select(t => new ProductKgDto(t.Key, BsLabels.Label(t.Key), t.Sum(x => x.QuantityKg)))
                 .ToList()))
            .OrderBy(x => x.Name, NaturalComparer.Instance)
            .ToList();

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
            HarvestsByProduct        = harvestsByProduct,
            HiveProductsByApiary     = hiveProductsByApiary,
            HiveProductsByPasture    = hiveProductsByPasture,
            HiveProductsByBeehive    = hiveProductsByBeehive,
        };
    }

    /// <summary>Kg per product, in enum order — each product on its own.</summary>
    private static IReadOnlyList<ProductKgDto> ProductItems(IEnumerable<Harvest> harvests) =>
        harvests
            .GroupBy(h => h.ProductType)
            .OrderBy(g => g.Key)
            .Select(g => new ProductKgDto(g.Key, BsLabels.Label(g.Key), g.Sum(HarvestTotals.TotalKg)))
            .ToList();

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
