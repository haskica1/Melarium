using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Harvests;
using Melarium.Application.Features.Harvests.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Locks the harvest rules (SPEC-02, widened in SPEC-30): entries must belong to the chosen apiary (→ 400);
/// a Beekeeper only ever sees records containing one of their assigned hives — never one kept as a single
/// figure, never through a lock; a record of the whole organization is its owner's to write; honey stays on
/// every plan while the other products need Standard or above to write; and a client older than SPEC-30
/// keeps getting honey only.
/// </summary>
public class HarvestServiceTests
{
    private const int OrgId = 1;
    private const int ApiaryA = 1;
    private const int ApiaryB = 2;

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAccessGuard _access = Substitute.For<IAccessGuard>();
    private readonly IPlanGuard _planGuard = Substitute.For<IPlanGuard>();

    private static ICurrentUser OrgAdmin => new TestCurrentUser { UserId = 1, Role = UserRole.OrganizationAdmin, OrganizationId = OrgId };
    private static ICurrentUser ApiaryAdmin => new TestCurrentUser { UserId = 2, Role = UserRole.ApiaryAdmin, OrganizationId = OrgId, ApiaryId = ApiaryA };
    private static ICurrentUser Beekeeper => new TestCurrentUser { UserId = 7, Role = UserRole.Beekeeper, OrganizationId = OrgId };

    private static Beehive Hive(int id) => new() { Id = id, Name = $"Košnica {id}", ApiaryId = ApiaryA };

    private static Harvest Honey(int id, int apiaryId, params int[] hiveIds) => new()
    {
        Id = id, OrganizationId = OrgId, ApiaryId = apiaryId, Date = DateTime.UtcNow,
        ProductType = HiveProductType.Honey, HoneyType = HoneyType.Acacia,
        Entries = hiveIds.Select(h => new HarvestEntry { BeehiveId = h, QuantityKg = 5m }).ToList(),
    };

    private static Harvest Split(int id, int apiaryId, HiveProductType product, params int[] hiveIds) => new()
    {
        Id = id, OrganizationId = OrgId, ApiaryId = apiaryId, Date = DateTime.UtcNow, ProductType = product,
        Entries = hiveIds.Select(h => new HarvestEntry { BeehiveId = h, QuantityKg = 0.1m }).ToList(),
    };

    private static Harvest Bulk(int id, int? apiaryId, HiveProductType product = HiveProductType.Wax, decimal kg = 5m, decimal? price = null) => new()
    {
        Id = id, OrganizationId = OrgId, ApiaryId = apiaryId, Date = DateTime.UtcNow,
        ProductType = product, BulkKg = kg, PricePerKg = price,
        HoneyType = product == HiveProductType.Honey ? HoneyType.Linden : null,
    };

    private HarvestService Service(ICurrentUser user, IPlanLock? planLock = null)
    {
        _uow.Apiaries.GetByIdAsync(ApiaryA).Returns(new Apiary { Id = ApiaryA, OrganizationId = OrgId, Name = "Gornji" });
        return new HarvestService(_uow, _access, user, planLock ?? TestPlanLock.Unlocked(), _planGuard);
    }

    private void FreePlan() =>
        _planGuard.EnsureFeatureAsync(Arg.Any<int>(), PlanFeature.HiveProducts)
            .Returns(Task.FromException(new PlanLimitException("Standard")));

    private Func<Harvest?> CaptureAdded()
    {
        Harvest? saved = null;
        _uow.Harvests.When(r => r.AddAsync(Arg.Any<Harvest>())).Do(ci => saved = ci.Arg<Harvest>());
        _uow.Harvests.GetWithEntriesAsync(Arg.Any<int>()).Returns(_ => saved);
        return () => saved;
    }

    private void WithEntries(params Harvest[] harvests)
    {
        foreach (var h in harvests)
            _uow.Harvests.GetWithEntriesAsync(h.Id).Returns(h);
    }

    // ── Writing ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithHiveNotInApiary_ThrowsValidation()
    {
        _uow.Beehives.GetByApiaryIdAsync(ApiaryA).Returns(new[] { Hive(5) }); // apiary only has hive 5

        var dto = new CreateHarvestDto
        {
            ApiaryId = ApiaryA, Date = DateTime.UtcNow, HoneyType = HoneyType.Acacia,
            Entries = [new CreateHarvestEntryDto { BeehiveId = 99, QuantityKg = 3m }], // hive 99 not in apiary
        };

        await Assert.ThrowsAsync<ValidationException>(() => Service(OrgAdmin).CreateAsync(dto));
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Create_HoneyOnFreePlan_IsAllowed_AndAClientWithoutAProductMeansHoney()
    {
        // Honey has been on every plan since SPEC-02; a client older than SPEC-30 sends no product.
        FreePlan();
        var saved = CaptureAdded();
        _uow.Beehives.GetByApiaryIdAsync(ApiaryA).Returns(new[] { Hive(5) });

        await Service(OrgAdmin).CreateAsync(new CreateHarvestDto
        {
            ApiaryId = ApiaryA, Date = DateTime.UtcNow, HoneyType = HoneyType.Acacia,
            Entries = [new CreateHarvestEntryDto { BeehiveId = 5, QuantityKg = 20m, FramesExtracted = 8 }],
        });

        Assert.Equal(HiveProductType.Honey, saved()!.ProductType);
        Assert.Equal(OrgId, saved()!.OrganizationId);
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Create_OtherProductOnFreePlan_IsRefused_NothingSaved()
    {
        FreePlan();

        var dto = new CreateHarvestDto
        {
            ApiaryId = ApiaryA, Date = DateTime.UtcNow, ProductType = HiveProductType.Pollen, BulkKg = 2m,
        };

        await Assert.ThrowsAsync<PlanLimitException>(() => Service(OrgAdmin).CreateAsync(dto));
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Create_ForTheWholeOrganization_AsOwner_HasNoApiary_HoneyIncluded()
    {
        var saved = CaptureAdded();

        await Service(OrgAdmin).CreateAsync(new CreateHarvestDto
        {
            ApiaryId = null, Date = DateTime.UtcNow, ProductType = HiveProductType.Honey,
            HoneyType = HoneyType.Meadow, BulkKg = 120m,
        });

        Assert.Null(saved()!.ApiaryId);
        Assert.Equal(OrgId, saved()!.OrganizationId);
        Assert.Equal(120m, saved()!.BulkKg);
    }

    [Fact]
    public async Task Create_ForTheWholeOrganization_AsApiaryAdmin_IsForbidden()
    {
        var dto = new CreateHarvestDto
        {
            ApiaryId = null, Date = DateTime.UtcNow, ProductType = HiveProductType.Wax, BulkKg = 3m,
        };

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service(ApiaryAdmin).CreateAsync(dto));
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Create_OtherProduct_DropsHoneyTypeAndFrames()
    {
        var saved = CaptureAdded();
        _uow.Beehives.GetByApiaryIdAsync(ApiaryA).Returns(new[] { Hive(5) });

        await Service(OrgAdmin).CreateAsync(new CreateHarvestDto
        {
            ApiaryId = ApiaryA, Date = DateTime.UtcNow, ProductType = HiveProductType.Propolis,
            HoneyType = HoneyType.Acacia,
            Entries = [new CreateHarvestEntryDto { BeehiveId = 5, QuantityKg = 0.12m, FramesExtracted = 4 }],
        });

        Assert.Null(saved()!.HoneyType);
        Assert.Null(Assert.Single(saved()!.Entries).FramesExtracted);
    }

    [Fact]
    public async Task Update_OnFreePlan_RefusesEditingAnOtherProduct_AndTurningHoneyIntoOne()
    {
        FreePlan();
        WithEntries(Bulk(10, ApiaryA, HiveProductType.Wax), Bulk(11, ApiaryA, HiveProductType.Honey));

        var keepWax = new UpdateHarvestDto { Date = DateTime.UtcNow, BulkKg = 6m };
        var honeyToWax = new UpdateHarvestDto { Date = DateTime.UtcNow, ProductType = HiveProductType.Wax, BulkKg = 6m };

        await Assert.ThrowsAsync<PlanLimitException>(() => Service(OrgAdmin).UpdateAsync(10, keepWax));
        await Assert.ThrowsAsync<PlanLimitException>(() => Service(OrgAdmin).UpdateAsync(11, honeyToWax));
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Update_SplittingARecordOfTheWholeOrganizationPerHive_ThrowsValidation()
    {
        WithEntries(Bulk(10, apiaryId: null));

        var dto = new UpdateHarvestDto
        {
            Date = DateTime.UtcNow, ProductType = HiveProductType.Wax,
            Entries = [new CreateHarvestEntryDto { BeehiveId = 5, QuantityKg = 1m }],
        };

        await Assert.ThrowsAsync<ValidationException>(() => Service(OrgAdmin).UpdateAsync(10, dto));
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Update_HoneyWithoutHoneyType_ThrowsValidation()
    {
        // No product sent = keep the record's own, which is honey here — so the honey type is required.
        WithEntries(Bulk(11, ApiaryA, HiveProductType.Honey));

        var dto = new UpdateHarvestDto { Date = DateTime.UtcNow, BulkKg = 30m };

        await Assert.ThrowsAsync<ValidationException>(() => Service(OrgAdmin).UpdateAsync(11, dto));
    }

    [Fact]
    public async Task Delete_IsNotPlanGated()
    {
        // Free reads and deletes what it recorded before; only adding and editing are refused.
        FreePlan();
        _uow.Harvests.GetByIdAsync(10).Returns(Bulk(10, ApiaryA));

        await Service(OrgAdmin).DeleteAsync(10);

        await _uow.Received(1).SaveChangesAsync();
    }

    // ── Reading ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_WithoutAProduct_IsHoneyOnly_SoAnOlderClientNeverMeetsWax()
    {
        _uow.Harvests.GetByOrganizationAsync(OrgId, Arg.Any<HarvestKind>(), Arg.Any<int?>()).Returns(new[] { Honey(1, ApiaryA, 5) });

        await Service(OrgAdmin).GetAllAsync(null, null, null);
        await Service(OrgAdmin).GetAllAsync(null, null, null, HarvestKind.All);

        await _uow.Harvests.Received(1).GetByOrganizationAsync(OrgId, HarvestKind.Honey, Arg.Any<int?>());
        await _uow.Harvests.Received(1).GetByOrganizationAsync(OrgId, HarvestKind.All, Arg.Any<int?>());
    }

    [Fact]
    public async Task GetAll_ForOneProduct_ReturnsThatProductOnly()
    {
        _uow.Harvests.GetByOrganizationAsync(OrgId, HarvestKind.OtherProducts, Arg.Any<int?>())
            .Returns(new[] { Bulk(1, ApiaryA, HiveProductType.Wax), Bulk(2, ApiaryA, HiveProductType.Pollen) });

        var result = (await Service(OrgAdmin).GetAllAsync(null, null, null, productType: HiveProductType.Pollen)).ToList();

        Assert.Equal(2, Assert.Single(result).Id);
    }

    [Fact]
    public async Task GetAll_AsBeekeeper_ReturnsOnlyRecordsContainingAssignedHives_NeverOneFigureOnes()
    {
        _access.GetAssignedBeehiveIdsAsync().Returns(new HashSet<int> { 5 });
        _access.GetAssignedApiaryIdsAsync().Returns(new HashSet<int> { ApiaryA });
        _uow.Harvests.GetByApiaryAsync(ApiaryA, HarvestKind.All, Arg.Any<int?>())
            .Returns(new[] { Honey(1, ApiaryA, 5, 6), Honey(2, ApiaryA, 8, 9), Bulk(3, ApiaryA, HiveProductType.Honey) });

        var result = (await Service(Beekeeper).GetAllAsync(null, null, null, HarvestKind.All)).ToList();

        var only = Assert.Single(result);
        Assert.Equal(1, only.Id);
        Assert.Equal(10m, only.TotalKg);   // the whole record, not just the beekeeper's line
    }

    [Fact]
    public async Task GetAll_AsBeekeeper_WithNoAssignments_ReturnsEmpty()
    {
        _access.GetAssignedBeehiveIdsAsync().Returns(new HashSet<int>());

        Assert.Empty(await Service(Beekeeper).GetAllAsync(null, null, null));
    }

    [Fact]
    public async Task GetAll_AsBeekeeper_InALockedApiary_SeesNothing()
    {
        // Lists older than SPEC-30 never asked the lock on this path; this one must.
        _access.GetAssignedBeehiveIdsAsync().Returns(new HashSet<int> { 5 });
        _access.GetAssignedApiaryIdsAsync().Returns(new HashSet<int> { ApiaryA });
        _uow.Harvests.GetByApiaryAsync(ApiaryA, Arg.Any<HarvestKind>(), Arg.Any<int?>()).Returns(new[] { Honey(1, ApiaryA, 5) });

        var locked = TestPlanLock.Locking(apiaryIds: [ApiaryA], beehiveIds: [5]);

        Assert.Empty(await Service(Beekeeper, locked).GetAllAsync(null, null, null));
    }

    [Fact]
    public async Task GetAll_AsOrgAdmin_DropsLockedApiary_KeepsTheOrganizationsOwn()
    {
        _uow.Harvests.GetByOrganizationAsync(OrgId, Arg.Any<HarvestKind>(), Arg.Any<int?>())
            .Returns(new[] { Bulk(1, ApiaryA), Bulk(2, ApiaryB), Bulk(3, apiaryId: null) });

        var locked = TestPlanLock.Locking(apiaryIds: [ApiaryB], beehiveIds: []);
        var ids = (await Service(OrgAdmin, locked).GetAllAsync(null, null, null, HarvestKind.All)).Select(r => r.Id).ToList();

        Assert.Equal([1, 3], ids);
    }

    [Fact]
    public async Task GetAll_AsApiaryAdmin_IncludesTheOrganizationsOwnRecords()
    {
        _uow.Harvests.GetByApiaryAsync(ApiaryA, Arg.Any<HarvestKind>(), Arg.Any<int?>()).Returns(new[] { Bulk(1, ApiaryA) });
        _uow.Harvests.GetSharedAsync(OrgId, Arg.Any<HarvestKind>(), Arg.Any<int?>()).Returns(new[] { Bulk(2, apiaryId: null) });

        var ids = (await Service(ApiaryAdmin).GetAllAsync(null, null, null, HarvestKind.All)).Select(r => r.Id).OrderBy(i => i).ToList();

        Assert.Equal([1, 2], ids);
    }

    [Fact]
    public async Task GetById_RecordOfTheWholeOrganization_AsBeekeeper_IsForbidden()
    {
        WithEntries(Bulk(3, apiaryId: null));

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service(Beekeeper).GetByIdAsync(3));
    }

    [Fact]
    public async Task Totals_AreDerived_OneFigureOrSumOfEntries_RevenueOnlyWithAPrice()
    {
        _uow.Harvests.GetByOrganizationAsync(OrgId, Arg.Any<HarvestKind>(), Arg.Any<int?>())
            .Returns(new[] { Bulk(1, ApiaryA, kg: 2.5m, price: 20m), Split(2, ApiaryA, HiveProductType.Propolis, 5, 6) });

        var result = (await Service(OrgAdmin).GetAllAsync(null, null, null, HarvestKind.All)).ToDictionary(r => r.Id);

        Assert.Equal(2.5m, result[1].TotalKg);
        Assert.Equal(50m, result[1].EstimatedRevenue);
        Assert.Equal("Vosak", result[1].ProductTypeName);
        Assert.Equal(string.Empty, result[1].HoneyTypeName);
        Assert.Equal(0.2m, result[2].TotalKg);
        Assert.Null(result[2].EstimatedRevenue);   // no price → no invented revenue
    }

    [Fact]
    public async Task HiveYield_IsHoneyOnly()
    {
        var year = DateTime.UtcNow.Year;
        _uow.Beehives.ExistsAsync(5).Returns(true);
        _uow.Harvests.GetHiveTotalsByYearAsync(5).Returns(new Dictionary<(int Year, HiveProductType ProductType), decimal>
        {
            [(year, HiveProductType.Honey)] = 10m,
            [(year, HiveProductType.Pollen)] = 3m,
        });

        var yield = await Service(OrgAdmin).GetHiveYieldAsync(5);
        var summary = await Service(OrgAdmin).GetHiveSummaryAsync(5);

        Assert.Equal(10m, yield.CurrentSeasonKg);
        Assert.Equal(2, Assert.Single(summary.ByYear).Items.Count);
    }
}
