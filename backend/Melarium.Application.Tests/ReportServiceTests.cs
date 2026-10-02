using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Reports;
using Melarium.Application.Features.Reports.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Locks the SPEC-25 aggregation rules. The three that would be invisible bugs in a document a
/// beekeeper hands to a municipality: unpriced kg must not disappear into a smaller revenue figure
/// (D6), currencies must never be added together (D7), and an apiary the plan locked away must not
/// appear at all (D12).
/// </summary>
public class ReportServiceTests
{
    private const int OrgId = 7;
    private const int ApiaryA = 1;
    private const int ApiaryB = 2;

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAccessGuard _access = Substitute.For<IAccessGuard>();

    private static Apiary ApiaryRow(int id, string name) => new() { Id = id, Name = name, OrganizationId = OrgId };

    private static Harvest HarvestRow(int apiaryId, DateTime date, decimal kg, decimal? pricePerKg, HoneyType type = HoneyType.Acacia) =>
        new()
        {
            OrganizationId = OrgId, ApiaryId = apiaryId, Date = date,
            ProductType = HiveProductType.Honey, HoneyType = type, PricePerKg = pricePerKg,
            Entries = [new HarvestEntry { BeehiveId = 100 + apiaryId, QuantityKg = kg }],
        };

    private static Harvest ProductRow(
        int? apiaryId, DateTime date, decimal kg, decimal? pricePerKg, HiveProductType type = HiveProductType.Propolis) =>
        new()
        {
            OrganizationId = OrgId, ApiaryId = apiaryId, Date = date, ProductType = type,
            PricePerKg = pricePerKg, BulkKg = kg,
        };

    private static Expense ExpenseRow(int? apiaryId, DateTime date, decimal amount, string currency = "BAM") =>
        new()
        {
            OrganizationId = OrgId, ApiaryId = apiaryId, PurchaseDate = date,
            TotalAmount = amount, Currency = currency, Items = [],
        };

    private ReportService Service(ICurrentUser user)
    {
        // Unstubbed Task<IEnumerable<T>> returns null from NSubstitute, so every collection the
        // service touches has to be stubbed even when the test does not care about it.
        _uow.Organizations.GetByIdAsync(OrgId).Returns(new Organization { Id = OrgId, Name = "Golden Hive" });
        _uow.ApiaryMoves.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>()).Returns([]);
        _uow.Diets.GetByApiaryIdsAsync(Arg.Any<IEnumerable<int>>()).Returns([]);
        _uow.Beehives.GetMergedByApiaryIdAsync(Arg.Any<int>()).Returns([]);
        _uow.Treatments.GetByApiaryIdsAsync(Arg.Any<IEnumerable<int>>()).Returns([]);
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<HarvestKind>(), Arg.Any<int?>()).Returns([]);
        _uow.Harvests.GetSharedAsync(Arg.Any<int?>(), Arg.Any<HarvestKind>(), Arg.Any<int?>()).Returns([]);
        _uow.Expenses.GetByOrganizationInRangeAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<DateTime>()).Returns([]);
        _access.GetAccessibleBeehivesAsync(Arg.Any<bool>()).Returns([]);
        _access.GetAccessibleApiariesAsync(Arg.Any<bool>()).Returns([]);

        return new ReportService(_uow, _access, user, Substitute.For<IConfiguration>());
    }

    private static ICurrentUser OrgAdmin => new TestCurrentUser
    {
        UserId = 1, Role = UserRole.OrganizationAdmin, OrganizationId = OrgId,
    };

    private static SeasonReportQueryDto Year2026(int? apiaryId = null) => new()
    {
        From = new DateOnly(2026, 1, 1), To = new DateOnly(2026, 12, 31), ApiaryId = apiaryId,
    };

    private static DateTime Day(int month, int day) => new(2026, month, day, 0, 0, 0, DateTimeKind.Utc);

    // ── Scope ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CallerWithoutOrganization_IsForbidden()
    {
        var service = Service(new TestCurrentUser { UserId = 1, Role = UserRole.SystemAdmin });

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            service.GetSeasonReportAsync(Year2026()));
    }

    [Fact]
    public async Task LockedApiary_IsAbsentFromEveryPartOfTheReport()
    {
        // The guard already drops locked apiaries (SPEC-24), and every collection is keyed off what
        // it returns — so a harvest on the locked apiary can never reach the document.
        var service = Service(OrgAdmin);
        _access.GetAccessibleApiariesAsync(Arg.Any<bool>()).Returns([ApiaryRow(ApiaryA, "Gornji")]);
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), HarvestKind.Honey, Arg.Any<int?>()).Returns(
        [
            HarvestRow(ApiaryA, Day(6, 1), 40m, 12m),
        ]);

        var report = await service.GetSeasonReportAsync(Year2026());

        Assert.Equal(["Gornji"], report.Header.ApiaryNames);
        Assert.Single(report.Yield.ByApiary);

        // Confirms it is the guard's answer, not a repository filter, that bounds the query.
        await _uow.Harvests.Received(1).GetByApiariesAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(ApiaryA)), HarvestKind.Honey, Arg.Any<int?>());
    }

    [Fact]
    public async Task NoAccessibleApiaries_ReturnsEmptyReportNotAnError()
    {
        var report = await Service(OrgAdmin).GetSeasonReportAsync(Year2026());

        Assert.Equal(0m, report.Yield.TotalKg);
        Assert.Equal(0m, report.Balance.NetBam);
        Assert.Empty(report.Header.ApiaryNames);
        Assert.Equal("Golden Hive", report.Header.OrganizationName);
    }

    // ── Yield (D6) ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task UnpricedKg_AreCountedSeparatelyAndNotInRevenue()
    {
        var service = Service(OrgAdmin);
        _access.GetAccessibleApiariesAsync(Arg.Any<bool>()).Returns([ApiaryRow(ApiaryA, "Gornji")]);
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), HarvestKind.Honey, Arg.Any<int?>()).Returns(
        [
            HarvestRow(ApiaryA, Day(6, 1), 100m, 12m),    // priced
            HarvestRow(ApiaryA, Day(7, 1), 200m, null),   // no price — the silent hole
        ]);

        var report = await service.GetSeasonReportAsync(Year2026());

        Assert.Equal(300m, report.Yield.TotalKg);
        Assert.Equal(100m, report.Yield.PricedKg);
        Assert.Equal(200m, report.Yield.UnpricedKg);
        Assert.Equal(1200m, report.Balance.EstimatedRevenueBam);
        Assert.Equal(200m, report.Notes.UnpricedKg);
    }

    [Fact]
    public async Task HarvestOutsideThePeriod_IsExcluded()
    {
        var service = Service(OrgAdmin);
        _access.GetAccessibleApiariesAsync(Arg.Any<bool>()).Returns([ApiaryRow(ApiaryA, "Gornji")]);
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), HarvestKind.Honey, Arg.Any<int?>()).Returns(
        [
            HarvestRow(ApiaryA, Day(9, 15), 50m, 10m),
            HarvestRow(ApiaryA, new DateTime(2025, 9, 15, 0, 0, 0, DateTimeKind.Utc), 999m, 10m),
        ]);

        var report = await service.GetSeasonReportAsync(Year2026());

        Assert.Equal(50m, report.Yield.TotalKg);
        Assert.Equal(1, report.Yield.HarvestCount);
    }

    // ── Expenses (D1, D7) ──────────────────────────────────────────────────────

    [Fact]
    public async Task Currencies_AreGroupedNeverSummed_AndOnlyBamEntersTheBalance()
    {
        var service = Service(OrgAdmin);
        _access.GetAccessibleApiariesAsync(Arg.Any<bool>()).Returns([ApiaryRow(ApiaryA, "Gornji")]);
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), HarvestKind.Honey, Arg.Any<int?>()).Returns(
        [
            HarvestRow(ApiaryA, Day(6, 1), 100m, 10m),   // 1000 BAM revenue
        ]);
        _uow.Expenses.GetByOrganizationInRangeAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<DateTime>()).Returns(
        [
            ExpenseRow(ApiaryA, Day(3, 1), 200m),
            ExpenseRow(ApiaryA, Day(4, 1), 50m, "EUR"),
        ]);

        var report = await service.GetSeasonReportAsync(Year2026());

        Assert.Equal(2, report.Expenses.ByCurrency.Count);
        Assert.Equal(200m, report.Expenses.ByCurrency.Single(c => c.Currency == "BAM").Amount);
        Assert.Equal(50m, report.Expenses.ByCurrency.Single(c => c.Currency == "EUR").Amount);

        // 250 would be the lie: KM and EUR added together.
        Assert.Equal(200m, report.Balance.TotalExpenseBam);
        Assert.Equal(800m, report.Balance.NetBam);
        Assert.Equal(["EUR"], report.Notes.NonBamCurrencies);
    }

    [Fact]
    public async Task SharedExpenses_StayOutOfPerApiaryBalanceButAreReported()
    {
        var service = Service(OrgAdmin);
        _access.GetAccessibleApiariesAsync(Arg.Any<bool>()).Returns([ApiaryRow(ApiaryA, "Gornji")]);
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), HarvestKind.Honey, Arg.Any<int?>()).Returns(
        [
            HarvestRow(ApiaryA, Day(6, 1), 100m, 10m),
        ]);
        _uow.Expenses.GetByOrganizationInRangeAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<DateTime>()).Returns(
        [
            ExpenseRow(ApiaryA, Day(3, 1), 200m),
            ExpenseRow(null, Day(3, 2), 300m),   // shared: bought for the whole operation
        ]);

        var report = await service.GetSeasonReportAsync(Year2026());

        var apiary = Assert.Single(report.Balance.ByApiary);
        Assert.Equal(200m, apiary.ExpenseBam);          // the shared 300 is not spread onto it
        Assert.Equal(800m, apiary.NetBam);

        Assert.Equal(300m, report.Expenses.SharedByCurrency.Single().Amount);
        Assert.Equal(500m, report.Balance.TotalExpenseBam);  // but it does count org-wide
        Assert.Equal(1, report.Notes.UnassignedExpenseCount);
    }

    [Fact]
    public async Task ExpensePinnedToAnUnreachableApiary_IsDropped_SharedOneIsNot()
    {
        // ApiaryB is out of scope (another apiary admin's, or locked). Its expense is not this
        // report's to show — but the shared row belongs to the organization and always counts.
        var service = Service(OrgAdmin);
        _access.GetAccessibleApiariesAsync(Arg.Any<bool>()).Returns([ApiaryRow(ApiaryA, "Gornji")]);
        _uow.Expenses.GetByOrganizationInRangeAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<DateTime>()).Returns(
        [
            ExpenseRow(ApiaryB, Day(3, 1), 999m),
            ExpenseRow(null, Day(3, 2), 40m),
        ]);

        var report = await service.GetSeasonReportAsync(Year2026());

        Assert.Equal(1, report.Expenses.Count);
        Assert.Equal(40m, report.Balance.TotalExpenseBam);
        Assert.Empty(report.Expenses.ByApiary);
    }

    // ── Other hive products (SPEC-30) ──────────────────────────────────────────

    [Fact]
    public async Task ProductRevenue_EntersTheBalance_UnpricedQuantityIsNotedNotCounted()
    {
        var service = Service(OrgAdmin);
        _access.GetAccessibleApiariesAsync(Arg.Any<bool>()).Returns([ApiaryRow(ApiaryA, "Gornji")]);
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), HarvestKind.Honey, Arg.Any<int?>()).Returns(
        [
            HarvestRow(ApiaryA, Day(6, 1), 100m, 10m),                                   // 1000 KM honey
        ]);
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), HarvestKind.OtherProducts, Arg.Any<int?>()).Returns(
        [
            ProductRow(ApiaryA, Day(7, 1), 2m, 150m),                                     // 300 KM propolis
            ProductRow(ApiaryA, Day(7, 2), 0.35m, null),                                  // no price
            ProductRow(ApiaryA, Day(8, 1), 0.2m, 4000m, HiveProductType.RoyalJelly),       // 800 KM
        ]);
        _uow.Expenses.GetByOrganizationInRangeAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<DateTime>()).Returns(
        [
            ExpenseRow(ApiaryA, Day(3, 1), 400m),
        ]);

        var report = await service.GetSeasonReportAsync(Year2026());

        Assert.Equal(1000m, report.Balance.EstimatedRevenueBam);     // honey keeps its SPEC-25 meaning
        Assert.Equal(1100m, report.Balance.ProductRevenueBam);
        Assert.Equal(1700m, report.Balance.NetBam);                  // 1000 + 1100 − 400
        Assert.Equal(1100m, Assert.Single(report.Balance.ByApiary).ProductRevenueBam);

        var propolis = report.Harvests.ByProduct.Single(t => t.ProductType == HiveProductType.Propolis);
        Assert.Equal(2.35m, propolis.Kg);
        Assert.Equal(0.35m, propolis.UnpricedKg);
        Assert.Equal(300m, propolis.EstimatedRevenueBam);

        var unpriced = Assert.Single(report.Notes.Unpriced);
        Assert.Equal(HiveProductType.Propolis, unpriced.ProductType);
        Assert.Equal(0.35m, unpriced.Kg);
    }

    [Fact]
    public async Task SharedProducts_CountInTheTotalButInNoApiaryRow()
    {
        // Wax rendered from every apiary's combs at once: the organization's income, not an apiary's.
        var service = Service(OrgAdmin);
        _access.GetAccessibleApiariesAsync(Arg.Any<bool>()).Returns([ApiaryRow(ApiaryA, "Gornji")]);
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), HarvestKind.OtherProducts, Arg.Any<int?>()).Returns(
        [
            ProductRow(ApiaryA, Day(5, 1), 1m, 100m),
        ]);
        _uow.Harvests.GetSharedAsync(Arg.Any<int?>(), HarvestKind.OtherProducts, Arg.Any<int?>()).Returns(
        [
            ProductRow(null, Day(10, 1), 12m, 20m, HiveProductType.Wax),                  // 240 KM
        ]);

        var report = await service.GetSeasonReportAsync(Year2026());

        Assert.Equal(340m, report.Balance.ProductRevenueBam);
        Assert.Equal(100m, Assert.Single(report.Balance.ByApiary).ProductRevenueBam);
        Assert.Equal(1, report.Notes.SharedHarvestCount);

        Assert.Equal(2, report.Products.ByApiary.Count);
        Assert.Null(report.Products.ByApiary[^1].ApiaryId);           // the shared row comes last
        Assert.Equal("Zajedničko", report.Products.ByApiary[^1].ApiaryName);
    }

    [Fact]
    public async Task ProductOutsideThePeriod_IsExcluded()
    {
        var service = Service(OrgAdmin);
        _access.GetAccessibleApiariesAsync(Arg.Any<bool>()).Returns([ApiaryRow(ApiaryA, "Gornji")]);
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), HarvestKind.OtherProducts, Arg.Any<int?>()).Returns(
        [
            ProductRow(ApiaryA, Day(6, 1), 3m, null, HiveProductType.Pollen),
            ProductRow(ApiaryA, new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc), 99m, null, HiveProductType.Pollen),
        ]);

        var report = await service.GetSeasonReportAsync(Year2026());

        Assert.Equal(1, report.Products.RecordCount);
        Assert.Equal(3m, report.Harvests.ByProduct.Single().Kg);
    }

    [Fact]
    public async Task HoneyAsOneFigure_CountsInEveryTotal_ButNotInThePerHiveTable()
    {
        // SPEC-30: honey recorded for the apiary, and for the whole organization, without a hive split.
        var service = Service(OrgAdmin);
        _access.GetAccessibleApiariesAsync(Arg.Any<bool>()).Returns([ApiaryRow(ApiaryA, "Gornji")]);
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), HarvestKind.Honey, Arg.Any<int?>()).Returns(
        [
            HarvestRow(ApiaryA, Day(6, 1), 40m, 10m),                                         // per hive
            new Harvest { OrganizationId = OrgId, ApiaryId = ApiaryA, Date = Day(6, 2),
                          ProductType = HiveProductType.Honey, HoneyType = HoneyType.Linden,
                          BulkKg = 100m, PricePerKg = 10m },                                   // whole apiary
        ]);
        _uow.Harvests.GetSharedAsync(Arg.Any<int?>(), HarvestKind.Honey, Arg.Any<int?>()).Returns(
        [
            new Harvest { OrganizationId = OrgId, ApiaryId = null, Date = Day(6, 3),
                          ProductType = HiveProductType.Honey, HoneyType = HoneyType.Meadow,
                          BulkKg = 60m, PricePerKg = 10m },                                    // whole organization
        ]);

        var report = await service.GetSeasonReportAsync(Year2026());

        Assert.Equal(200m, report.Yield.TotalKg);
        var notPerHive = Assert.Single(report.Notes.NotPerHive);
        Assert.Equal((HiveProductType.Honey, 160m), (notPerHive.ProductType, notPerHive.Kg));
        Assert.Equal(40m, Assert.Single(report.Yield.ByBeehive).Kg);
        Assert.Equal(60m, report.Yield.ByApiary.Single(a => a.Name == "Zajedničko").Kg);

        Assert.Equal(2000m, report.Balance.EstimatedRevenueBam);                              // all of it
        Assert.Equal(1400m, Assert.Single(report.Balance.ByApiary).EstimatedRevenueBam);     // not the org's 600
        Assert.Equal(1, report.Notes.SharedHarvestCount);
    }

    [Fact]
    public async Task OtherProducts_NeverReachTheHoneyYield()
    {
        // Comb honey included: Asim chose to keep it out of the honey yield.
        var service = Service(OrgAdmin);
        _access.GetAccessibleApiariesAsync(Arg.Any<bool>()).Returns([ApiaryRow(ApiaryA, "Gornji")]);
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), HarvestKind.Honey, Arg.Any<int?>()).Returns(
        [
            HarvestRow(ApiaryA, Day(6, 1), 40m, null),
        ]);
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), HarvestKind.OtherProducts, Arg.Any<int?>()).Returns(
        [
            ProductRow(ApiaryA, Day(6, 2), 15m, null, HiveProductType.CombHoney),
            ProductRow(ApiaryA, Day(6, 3), 8m, null, HiveProductType.Wax),
        ]);

        var report = await service.GetSeasonReportAsync(Year2026());

        Assert.Equal(40m, report.Yield.TotalKg);
        Assert.Equal(2, report.Products.RecordCount);
        Assert.Equal(40m, report.Harvests.ByProduct.Single(t => t.ProductType == HiveProductType.Honey).Kg);
        Assert.Contains(report.Harvests.ByProduct, t => t.ProductType == HiveProductType.CombHoney && t.Kg == 15m);
    }

    // ── The "Prinosi" overview and the products' breakdowns (SPEC-30) ────────────

    [Fact]
    public async Task Overview_ListsEveryProductHoneyFirst_AndSumsOnlyRevenue()
    {
        var service = Service(OrgAdmin);
        _access.GetAccessibleApiariesAsync(Arg.Any<bool>()).Returns([ApiaryRow(ApiaryA, "Gornji")]);
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), HarvestKind.Honey, Arg.Any<int?>()).Returns(
        [
            HarvestRow(ApiaryA, Day(6, 1), 100m, 10m),                                       // 1000 KM
            HarvestRow(ApiaryA, Day(6, 2), 20m, null),                                       // no price
        ]);
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), HarvestKind.OtherProducts, Arg.Any<int?>()).Returns(
        [
            ProductRow(ApiaryA, Day(7, 1), 0.2m, 150m),                                      // propolis, 30 KM
            ProductRow(ApiaryA, Day(7, 2), 5m, null, HiveProductType.Wax),                   // no price
        ]);

        var report = await service.GetSeasonReportAsync(Year2026());

        // The enum decides the order, so honey leads and wax comes before propolis.
        Assert.Equal([HiveProductType.Honey, HiveProductType.Wax, HiveProductType.Propolis],
            report.Harvests.ByProduct.Select(p => p.ProductType));
        var honey = report.Harvests.ByProduct[0];
        Assert.Equal((120m, 20m, 2), (honey.Kg, honey.UnpricedKg, honey.RecordCount));

        // Revenue is the one figure added across products, and it is exactly the balance's revenue.
        Assert.Equal(1030m, report.Harvests.EstimatedRevenueBam);
        Assert.Equal(report.Balance.EstimatedRevenueBam + report.Balance.ProductRevenueBam, report.Harvests.EstimatedRevenueBam);

        // One note for everything without a price, honey included, in the same order.
        Assert.Equal([(HiveProductType.Honey, 20m), (HiveProductType.Wax, 5m)],
            report.Notes.Unpriced.Select(u => (u.ProductType, u.Kg)));
        Assert.Equal(20m, report.Notes.UnpricedKg);                                          // honey alone, for older clients
    }

    [Fact]
    public async Task Products_ByPasture_FollowTheApiaryMoves_OrganizationRecordsGetTheirOwnRow()
    {
        var service = Service(OrgAdmin);
        _access.GetAccessibleApiariesAsync(Arg.Any<bool>()).Returns([ApiaryRow(ApiaryA, "Gornji")]);
        var vlasic = new Pasture { Id = 30, Name = "Vlašić", OrganizationId = OrgId };
        _uow.ApiaryMoves.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>()).Returns(
        [
            new ApiaryMove { Id = 1, ApiaryId = ApiaryA, ToPastureId = vlasic.Id, ToPasture = vlasic, MovedAt = Day(5, 1) },
        ]);
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), HarvestKind.Honey, Arg.Any<int?>()).Returns(
        [
            HarvestRow(ApiaryA, Day(6, 1), 40m, null),
        ]);
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), HarvestKind.OtherProducts, Arg.Any<int?>()).Returns(
        [
            ProductRow(ApiaryA, Day(6, 1), 0.3m, null, HiveProductType.Pollen),              // on Vlašić
            ProductRow(ApiaryA, Day(4, 1), 2m, null, HiveProductType.Wax),                   // before the move: home
        ]);
        _uow.Harvests.GetSharedAsync(Arg.Any<int?>(), HarvestKind.OtherProducts, Arg.Any<int?>()).Returns(
        [
            ProductRow(null, Day(9, 1), 4m, null, HiveProductType.Wax),                      // the whole organization
        ]);

        var report = await service.GetSeasonReportAsync(Year2026());

        // Pastures first, then the home location, then the organization's own row.
        Assert.Equal(["Vlašić", "Matična lokacija", "Zajedničko"], report.Products.ByPasture.Select(r => r.Name));
        Assert.Equal((HiveProductType.Pollen, 0.3m), OnlyItem(report.Products.ByPasture[0]));
        Assert.Equal((HiveProductType.Wax, 2m), OnlyItem(report.Products.ByPasture[1]));
        Assert.Equal((HiveProductType.Wax, 4m), OnlyItem(report.Products.ByPasture[2]));

        // Honey keeps its own by-pasture table and never shows up in this one.
        var honeyPasture = Assert.Single(report.Yield.ByPasture);
        Assert.Equal(("Vlašić", 40m), (honeyPasture.Name, honeyPasture.Kg));
    }

    [Fact]
    public async Task Products_ByBeehive_ComeFromPerHiveLinesOnly_InHiveNumberOrder()
    {
        var service = Service(OrgAdmin);
        _access.GetAccessibleApiariesAsync(Arg.Any<bool>()).Returns([ApiaryRow(ApiaryA, "Gornji")]);
        _access.GetAccessibleBeehivesAsync(Arg.Any<bool>()).Returns(
        [
            new Beehive { Id = 110, Name = "K10", ApiaryId = ApiaryA },
            new Beehive { Id = 102, Name = "K2", ApiaryId = ApiaryA },
        ]);
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), HarvestKind.OtherProducts, Arg.Any<int?>()).Returns(
        [
            new Harvest
            {
                OrganizationId = OrgId, ApiaryId = ApiaryA, Date = Day(7, 1), ProductType = HiveProductType.Propolis,
                Entries = [new HarvestEntry { BeehiveId = 110, QuantityKg = 0.05m }, new HarvestEntry { BeehiveId = 102, QuantityKg = 0.08m }],
            },
            new Harvest
            {
                OrganizationId = OrgId, ApiaryId = ApiaryA, Date = Day(7, 2), ProductType = HiveProductType.RoyalJelly,
                Entries = [new HarvestEntry { BeehiveId = 102, QuantityKg = 0.01m }],
            },
            ProductRow(ApiaryA, Day(8, 1), 3m, null, HiveProductType.Wax),                  // one figure: no hive
        ]);

        var report = await service.GetSeasonReportAsync(Year2026());

        Assert.Equal(["K2", "K10"], report.Products.ByBeehive.Select(r => r.Name));         // K2 before K10
        Assert.Equal([(HiveProductType.Propolis, 0.08m), (HiveProductType.RoyalJelly, 0.01m)],
            report.Products.ByBeehive[0].Items.Select(i => (i.ProductType, i.Kg)));
        Assert.DoesNotContain(report.Products.ByBeehive, r => r.Items.Any(i => i.ProductType == HiveProductType.Wax));

        // The wax kept as one figure is stated instead, so the per-hive table is not read as all of it.
        var notPerHive = Assert.Single(report.Notes.NotPerHive);
        Assert.Equal((HiveProductType.Wax, 3m), (notPerHive.ProductType, notPerHive.Kg));
    }

    private static (HiveProductType, decimal) OnlyItem(NamedProductsReportDto row)
    {
        var item = Assert.Single(row.Items);
        return (item.ProductType, item.Kg);
    }

    // ── Treatments (D5) ────────────────────────────────────────────────────────

    [Fact]
    public async Task TreatmentCrossingThePeriodBoundary_BelongsToItsStartDateOnly()
    {
        var service = Service(OrgAdmin);
        _access.GetAccessibleApiariesAsync(Arg.Any<bool>()).Returns([ApiaryRow(ApiaryA, "Gornji")]);
        _uow.Treatments.GetByApiaryIdsAsync(Arg.Any<IEnumerable<int>>()).Returns(
        [
            new Treatment
            {
                ApiaryId = ApiaryA, ProductName = "Apiguard", ActiveSubstance = ActiveSubstance.Thymol,
                StartDate = Day(9, 25), EndDate = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc),
                WithdrawalDays = 0,
                Entries = [new TreatmentEntry { BeehiveId = 101 }, new TreatmentEntry { BeehiveId = 102 }],
            },
        ]);

        var september = await service.GetSeasonReportAsync(new SeasonReportQueryDto
        {
            From = new DateOnly(2026, 9, 1), To = new DateOnly(2026, 9, 30),
        });
        var october = await service.GetSeasonReportAsync(new SeasonReportQueryDto
        {
            From = new DateOnly(2026, 10, 1), To = new DateOnly(2026, 10, 31),
        });

        Assert.Equal(1, september.Treatments.Count);
        Assert.Equal(2, september.Treatments.HivesTreated);
        Assert.Equal("Apiguard", september.Treatments.ByProduct.Single().ProductName);

        // Not double-counted in the period it merely runs into — otherwise the four quarters would
        // no longer add up to the year.
        Assert.Equal(0, october.Treatments.Count);
    }
}
