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
            ApiaryId = apiaryId, Date = date, HoneyType = type, PricePerKg = pricePerKg,
            Entries = [new HarvestEntry { BeehiveId = 100 + apiaryId, QuantityKg = kg }],
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
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<int?>()).Returns([]);
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
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<int?>()).Returns(
        [
            HarvestRow(ApiaryA, Day(6, 1), 40m, 12m),
        ]);

        var report = await service.GetSeasonReportAsync(Year2026());

        Assert.Equal(["Gornji"], report.Header.ApiaryNames);
        Assert.Single(report.Yield.ByApiary);

        // Confirms it is the guard's answer, not a repository filter, that bounds the query.
        await _uow.Harvests.Received(1).GetByApiariesAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(ApiaryA)), Arg.Any<int?>());
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
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<int?>()).Returns(
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
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<int?>()).Returns(
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
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<int?>()).Returns(
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
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<int?>()).Returns(
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
