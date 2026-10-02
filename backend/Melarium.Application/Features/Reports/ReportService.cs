using Melarium.Application.Common;
using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Localization;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Reports.DTOs;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Features.Reports;

/// <summary>
/// Aggregates one consolidated report for an arbitrary period (SPEC-25).
///
/// <para>
/// Deliberately <b>not</b> an extension of <c>StatsService</c> (D10): that one answers "the current
/// year, the whole organization, on one screen" and this one answers "any period, optionally one
/// apiary, as a document". Sharing the aggregation would force both to carry the other's shape.
/// </para>
///
/// <para>
/// Scope comes from <see cref="IAccessGuard.GetAccessibleApiariesAsync"/>, which is already
/// role-scoped and already drops apiaries locked by a downgrade (SPEC-24). Every other collection
/// here is keyed off that set, so the lock is honoured once rather than in seven places (D12).
/// </para>
/// </summary>
public class ReportService : IReportService
{
    private const string Bam = "BAM";
    private const string SharedLabel = "Zajedničko";

    private readonly IUnitOfWork _uow;
    private readonly IAccessGuard _access;
    private readonly ICurrentUser _currentUser;
    private readonly TimeZoneInfo _tz;

    public ReportService(IUnitOfWork uow, IAccessGuard access, ICurrentUser currentUser, IConfiguration config)
    {
        _uow = uow;
        _access = access;
        _currentUser = currentUser;
        _tz = AppTimeZone.Resolve(config);
    }

    public async Task<SeasonReportDto> GetSeasonReportAsync(SeasonReportQueryDto query)
    {
        // A report is an organization's document. A SystemAdmin belongs to no organization, so there
        // is no "my expenses" for him to report on — same reasoning and same 403 as /organizations/my.
        var orgId = _currentUser.OrganizationId
            ?? throw new ForbiddenAccessException("Izvještaj postoji samo u okviru organizacije.");

        if (query.ApiaryId is int requested)
            await _access.EnsureCanManageApiaryAsync(requested);

        var apiaries = (await _access.GetAccessibleApiariesAsync())
            .Where(a => query.ApiaryId is not int id || a.Id == id)
            .OrderBy(a => a.Name)
            .ToList();

        var apiaryIds   = apiaries.Select(a => a.Id).ToList();
        var apiaryNames = apiaries.ToDictionary(a => a.Id, a => a.Name);

        var organization = await _uow.Organizations.GetByIdAsync(orgId);

        var header = new ReportHeaderDto
        {
            OrganizationName = organization?.Name ?? string.Empty,
            From             = query.From,
            To               = query.To,
            GeneratedAt      = DateTime.UtcNow,
            ApiaryNames      = apiaries.Select(a => a.Name).ToList(),
        };

        if (apiaryIds.Count == 0)
            return new SeasonReportDto { Header = header };

        // Honey and the other products share one table and are never summed together (SPEC-30).
        // Records of the whole organization join whichever apiaries are in scope, even a single one —
        // the same rule as a shared expense: they belong to the organization, which is in every report.
        var harvests = (await _uow.Harvests.GetByApiariesAsync(apiaryIds, HarvestKind.Honey))
            .Concat(await _uow.Harvests.GetSharedAsync(orgId, HarvestKind.Honey))
            .Where(h => InPeriod(query, h.Date))
            .ToList();

        var products = (await _uow.Harvests.GetByApiariesAsync(apiaryIds, HarvestKind.OtherProducts))
            .Concat(await _uow.Harvests.GetSharedAsync(orgId, HarvestKind.OtherProducts))
            .Where(p => InPeriod(query, p.Date))
            .ToList();

        var treatments = (await _uow.Treatments.GetByApiaryIdsAsync(apiaryIds))
            .Where(t => InPeriod(query, t.StartDate))   // D5: the start date decides, never a range overlap
            .ToList();

        var (fromUtc, toUtc) = ReportPeriod.UtcBounds(query.From, query.To);
        var expenses = (await _uow.Expenses.GetByOrganizationInRangeAsync(orgId, fromUtc, toUtc))
            .Where(e => InPeriod(query, e.PurchaseDate))
            // An expense pinned to an apiary the caller cannot reach is not theirs to see; a shared
            // one (null) belongs to the organization and always counts.
            .Where(e => e.ApiaryId is not int aid || apiaryNames.ContainsKey(aid))
            .ToList();

        // Shared by honey and the other products, so a hive or a pasture is named the same in both.
        var hiveNames = await BuildHiveNameMapAsync(apiaryIds);
        var pastures  = PastureBuckets.From(await _uow.ApiaryMoves.GetByApiariesAsync(apiaryIds));

        var yield      = BuildYield(harvests, apiaryNames, hiveNames, pastures);
        var expenseDto = await BuildExpensesAsync(expenses, apiaryNames, apiaryIds);
        var balance    = BuildBalance(harvests, products, expenses, apiaryNames);
        var everything = harvests.Concat(products).ToList();

        return new SeasonReportDto
        {
            Header     = header,
            Harvests   = BuildOverview(everything),
            Yield      = yield,
            Products   = BuildProducts(products, apiaryNames, hiveNames, pastures),
            Expenses   = expenseDto,
            Balance    = balance,
            Treatments = BuildTreatments(treatments, header.GeneratedAt),
            Notes      = new ReportNotesDto
            {
                UnpricedKg             = yield.UnpricedKg,
                UnassignedExpenseCount = expenses.Count(e => e.ApiaryId is null),
                NonBamCurrencies       = expenses
                    .Select(e => e.Currency)
                    .Where(c => !string.Equals(c, Bam, StringComparison.OrdinalIgnoreCase))
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList(),
                // One sentence for every product, honey included — the reader should not have to add
                // two notes together to learn what is missing from the revenue (SPEC-30).
                Unpriced   = ItemsOf(everything.Where(h => !h.PricePerKg.HasValue)).Where(x => x.Kg > 0).ToList(),
                // Recorded as one figure for an apiary or the organization: in every total, in no
                // per-hive table, which therefore adds up to less.
                NotPerHive = ItemsOf(everything.Where(h => h.BulkKg.HasValue)).Where(x => x.Kg > 0).ToList(),
                SharedHarvestCount = everything.Count(h => h.ApiaryId is null),
            },
        };
    }

    private bool InPeriod(SeasonReportQueryDto query, DateTime instant) =>
        ReportPeriod.Contains(query.From, query.To, instant, _tz);

    // ── Yield ──────────────────────────────────────────────────────────────────

    private static ReportYieldDto BuildYield(
        List<Harvest> harvests,
        Dictionary<int, string> apiaryNames,
        Dictionary<int, string> hiveNames,
        PastureBuckets? pastures)
    {
        static decimal KgOf(Harvest h) => HarvestTotals.TotalKg(h);

        var byApiary = harvests
            .GroupBy(h => h.ApiaryId)
            .Select(g => new NamedKgDto(g.Key is int id ? NameOf(apiaryNames, id, "Pčelinjak") : SharedLabel, g.Sum(KgOf)))
            .OrderByDescending(x => x.Kg)
            .ToList();

        var byHoneyType = harvests
            .GroupBy(h => h.HoneyType)
            .Select(g => new NamedKgDto(g.Key is HoneyType t ? BsLabels.Label(t) : "—", g.Sum(KgOf)))
            .OrderByDescending(x => x.Kg)
            .ToList();

        var byBeehive = harvests
            .SelectMany(h => h.Entries)
            .GroupBy(e => e.BeehiveId)
            .Select(g => new NamedKgDto(NameOf(hiveNames, g.Key, "Košnica"), g.Sum(e => e.QuantityKg)))
            .OrderByDescending(x => x.Kg)
            .ToList();

        // A record of the whole organization stood on no one pasture — it gets its own bucket rather
        // than being attributed to the home location of an apiary it does not belong to.
        var byPasture = pastures is null
            ? []
            : harvests
                .GroupBy(pastures.Of)
                .Select(g => new NamedKgDto(pastures.NameOf(g.Key), g.Sum(KgOf)))
                .OrderByDescending(x => x.Kg)
                .ToList();

        return new ReportYieldDto
        {
            TotalKg      = harvests.Sum(KgOf),
            PricedKg     = harvests.Where(h => h.PricePerKg.HasValue).Sum(KgOf),
            UnpricedKg   = harvests.Where(h => !h.PricePerKg.HasValue).Sum(KgOf),
            HarvestCount = harvests.Count,
            ByApiary     = byApiary,
            ByHoneyType  = byHoneyType,
            ByBeehive    = byBeehive,
            ByPasture    = byPasture,
        };
    }

    /// <summary>
    /// Hive names for the report, <b>including hives merged away</b> (SPEC-19): a merge never touches
    /// the harvest rows it left behind, so a historical report must still be able to name them. The
    /// accessible set alone would leave those lines reading "Košnica #42".
    /// </summary>
    private async Task<Dictionary<int, string>> BuildHiveNameMapAsync(List<int> apiaryIds)
    {
        var names = (await _access.GetAccessibleBeehivesAsync())
            .ToDictionary(b => b.Id, b => b.Name);

        foreach (var apiaryId in apiaryIds)
            foreach (var merged in await _uow.Beehives.GetMergedByApiaryIdAsync(apiaryId))
                names.TryAdd(merged.Id, merged.Name);

        return names;
    }

    // ── Prinosi overview and the other products (SPEC-30) ──────────────────────

    /// <summary>
    /// One row per product, honey included — the table that opens "Prinosi". Rows follow the enum, not
    /// the label, so honey leads and a translated client does not reshuffle them. Revenue is the only
    /// figure summed across the rows.
    /// </summary>
    private static ReportHarvestsDto BuildOverview(List<Harvest> everything) =>
        new()
        {
            ByProduct = everything
                .GroupBy(h => h.ProductType)
                .OrderBy(g => g.Key)
                .Select(g => new ProductTypeReportDto(
                    g.Key,
                    BsLabels.Label(g.Key),
                    g.Sum(HarvestTotals.TotalKg),
                    g.Where(h => h.PricePerKg.HasValue).Sum(HarvestTotals.TotalKg),
                    g.Where(h => !h.PricePerKg.HasValue).Sum(HarvestTotals.TotalKg),
                    g.Sum(h => HarvestTotals.EstimatedRevenue(h) ?? 0m),
                    g.Count()))
                .ToList(),
            EstimatedRevenueBam = everything.Sum(h => HarvestTotals.EstimatedRevenue(h) ?? 0m),
        };

    /// <summary>
    /// The honey breakdowns, for every other product: per apiary, per pasture and per hive. Every row
    /// lists each product on its own — never a total across types.
    /// </summary>
    private static ReportProductsDto BuildProducts(
        List<Harvest> products,
        Dictionary<int, string> apiaryNames,
        Dictionary<int, string> hiveNames,
        PastureBuckets? pastures) =>
        new()
        {
            RecordCount = products.Count,
            ByApiary = products
                .GroupBy(p => p.ApiaryId)
                .Select(g => new ApiaryProductsReportDto(
                    g.Key,
                    g.Key is int id ? NameOf(apiaryNames, id, "Pčelinjak") : SharedLabel,
                    ItemsOf(g)))
                .OrderBy(x => x.ApiaryId is null)
                .ThenBy(x => x.ApiaryName)
                .ToList(),
            ByPasture = pastures is null
                ? []
                : pastures.Order(products.GroupBy(pastures.Of), g => g.Key)
                    .Select(g => new NamedProductsReportDto(pastures.NameOf(g.Key), ItemsOf(g)))
                    .ToList(),
            // Per-hive lines only; one-figure records are in the notes. By name, since no single kg
            // can rank a row that holds several products.
            ByBeehive = products
                .SelectMany(p => p.Entries.Select(e => (e.BeehiveId, p.ProductType, e.QuantityKg)))
                .GroupBy(x => x.BeehiveId)
                .Select(g => new NamedProductsReportDto(
                    NameOf(hiveNames, g.Key, "Košnica"),
                    g.GroupBy(x => x.ProductType)
                     .OrderBy(t => t.Key)
                     .Select(t => new ProductKgReportDto(t.Key, BsLabels.Label(t.Key), t.Sum(x => x.QuantityKg)))
                     .ToList()))
                .OrderBy(x => x.Name, NaturalComparer.Instance)
                .ToList(),
        };

    /// <summary>Kg per product, in enum order — each product on its own.</summary>
    private static IReadOnlyList<ProductKgReportDto> ItemsOf(IEnumerable<Harvest> harvests) =>
        harvests
            .GroupBy(h => h.ProductType)
            .OrderBy(g => g.Key)
            .Select(g => new ProductKgReportDto(g.Key, BsLabels.Label(g.Key), g.Sum(HarvestTotals.TotalKg)))
            .ToList();

    // ── Expenses ───────────────────────────────────────────────────────────────

    private async Task<ReportExpensesDto> BuildExpensesAsync(
        List<Expense> expenses,
        Dictionary<int, string> apiaryNames,
        List<int> apiaryIds)
    {
        var byApiary = expenses
            .Where(e => e.ApiaryId.HasValue)
            .GroupBy(e => e.ApiaryId!.Value)
            .Select(g => new ApiaryExpenseDto(g.Key, NameOf(apiaryNames, g.Key, "Pčelinjak"), GroupByCurrency(g)))
            .OrderBy(x => x.ApiaryName)
            .ToList();

        return new ReportExpensesDto
        {
            Count            = expenses.Count,
            ByCurrency       = GroupByCurrency(expenses),
            ByApiary         = byApiary,
            SharedByCurrency = GroupByCurrency(expenses.Where(e => e.ApiaryId is null)),
            ByDiet           = await BuildByDietAsync(expenses, apiaryIds),
        };
    }

    private async Task<IReadOnlyList<DietExpenseDto>> BuildByDietAsync(List<Expense> expenses, List<int> apiaryIds)
    {
        var lines = expenses
            .SelectMany(e => e.Items.Where(i => i.DietId.HasValue)
                                    .Select(i => (DietId: i.DietId!.Value, e.Currency, i.TotalPrice)))
            .ToList();
        if (lines.Count == 0) return [];

        var dietNames = (await _uow.Diets.GetByApiaryIdsAsync(apiaryIds))
            .ToDictionary(d => d.Id, d => d.Name);

        return lines
            .GroupBy(l => l.DietId)
            .Select(g => new DietExpenseDto(
                g.Key,
                NameOf(dietNames, g.Key, "Program"),
                g.GroupBy(l => l.Currency)
                 .Select(cg => new CurrencyAmountDto(cg.Key, cg.Sum(l => l.TotalPrice)))
                 .OrderBy(c => c.Currency)
                 .ToList()))
            .OrderBy(x => x.DietName)
            .ToList();
    }

    /// <summary>
    /// Amounts grouped by currency, never summed across them (D7) — the rule
    /// <c>IExpenseRepository.GetTotalsByDietsAsync</c> already established: adding KM to EUR is a
    /// silent lie even when everything is, in practice, KM.
    /// </summary>
    private static IReadOnlyList<CurrencyAmountDto> GroupByCurrency(IEnumerable<Expense> expenses) =>
        expenses
            .GroupBy(e => e.Currency)
            .Select(g => new CurrencyAmountDto(g.Key, g.Sum(e => e.TotalAmount)))
            .OrderByDescending(c => c.Amount)
            .ToList();

    // ── Balance ────────────────────────────────────────────────────────────────

    /// <summary>
    /// BAM only (D7). Revenue is denominated in KM by construction — <c>Harvest.PricePerKg</c> is
    /// KM/kg with no currency of its own — so subtracting a euro expense from it would produce a
    /// number in no currency at all. Non-BAM expenses are reported, just not netted.
    /// </summary>
    private static ReportBalanceDto BuildBalance(
        List<Harvest> harvests,
        List<Harvest> products,
        List<Expense> expenses,
        Dictionary<int, string> apiaryNames)
    {
        // KM by construction for honey and the other products alike: PricePerKg is KM/kg.
        static decimal RevenueOf(IEnumerable<Harvest> hs) =>
            hs.Sum(h => HarvestTotals.EstimatedRevenue(h) ?? 0m);

        var bamExpenses = expenses
            .Where(e => string.Equals(e.Currency, Bam, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var revenue        = RevenueOf(harvests);
        var productRevenue = RevenueOf(products);
        var total          = bamExpenses.Sum(e => e.TotalAmount);

        // Only apiaries that actually show up on one side or the other — an apiary with neither a
        // harvest, a product nor an expense in the period is not a zero row, it is simply absent.
        // Records of the whole organization stay out of every row, as shared expenses do: the total
        // carries them.
        var byApiary = harvests.Where(h => h.ApiaryId.HasValue).Select(h => h.ApiaryId!.Value)
            .Concat(products.Where(p => p.ApiaryId.HasValue).Select(p => p.ApiaryId!.Value))
            .Concat(bamExpenses.Where(e => e.ApiaryId.HasValue).Select(e => e.ApiaryId!.Value))
            .Distinct()
            .Select(id =>
            {
                var hs = harvests.Where(h => h.ApiaryId == id).ToList();
                var apiaryRevenue        = RevenueOf(hs);
                var apiaryProductRevenue = RevenueOf(products.Where(p => p.ApiaryId == id));
                var apiaryExpense        = bamExpenses.Where(e => e.ApiaryId == id).Sum(e => e.TotalAmount);
                return new ApiaryBalanceDto(
                    id,
                    NameOf(apiaryNames, id, "Pčelinjak"),
                    hs.Sum(HarvestTotals.TotalKg),
                    apiaryRevenue,
                    apiaryProductRevenue,
                    apiaryExpense,
                    apiaryRevenue + apiaryProductRevenue - apiaryExpense);
            })
            .OrderByDescending(x => x.NetBam)
            .ToList();

        return new ReportBalanceDto
        {
            EstimatedRevenueBam = revenue,
            ProductRevenueBam   = productRevenue,
            TotalExpenseBam     = total,
            NetBam              = revenue + productRevenue - total,
            ByApiary            = byApiary,
        };
    }

    // ── Treatments ─────────────────────────────────────────────────────────────

    /// <summary>
    /// A summary, not the register (SPEC-25 out of scope): the full legal register already exists as
    /// its own PDF per apiary and year, and a second copy of it is a second thing that can disagree.
    /// </summary>
    private static ReportTreatmentsDto BuildTreatments(List<Treatment> treatments, DateTime asOf) =>
        new()
        {
            Count        = treatments.Count,
            HivesTreated = treatments.SelectMany(t => t.Entries).Select(e => e.BeehiveId).Distinct().Count(),
            ActiveKarencaCount = treatments.Count(t =>
                TreatmentStatusHelper.Status(t.StartDate, t.EndDate, t.WithdrawalDays, asOf)
                    == Domain.Enums.TreatmentStatus.Karenca),
            ByProduct = treatments
                .GroupBy(t => new { t.ProductName, t.ActiveSubstance })
                .Select(g => new TreatmentProductDto(
                    g.Key.ProductName,
                    BsLabels.Label(g.Key.ActiveSubstance),
                    g.Count(),
                    g.SelectMany(t => t.Entries).Select(e => e.BeehiveId).Distinct().Count()))
                .OrderByDescending(x => x.TreatmentCount)
                .ThenBy(x => x.ProductName)
                .ToList(),
        };

    private static string NameOf(IReadOnlyDictionary<int, string> names, int id, string fallbackPrefix) =>
        names.TryGetValue(id, out var name) ? name : $"{fallbackPrefix} #{id}";
}
