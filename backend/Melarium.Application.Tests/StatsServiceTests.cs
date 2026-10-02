using System.Linq.Expressions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Stats;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// The SPEC-30 risk, locked: honey and the other products share one table, so the honey figures on the
/// stats page must read honey alone — kept as one figure or for the whole organization included — and
/// never pick up wax, pollen or comb honey, which have their own section.
/// </summary>
public class StatsServiceTests
{
    private const int OrgId = 1;
    private const int ApiaryA = 1;

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private StatsService Service(IPlanLock? planLock = null)
    {
        // Unstubbed Task<IEnumerable<T>> returns null from NSubstitute, so every collection is stubbed.
        _uow.Apiaries.GetAllByOrganizationAsync(OrgId).Returns(new[] { new Apiary { Id = ApiaryA, Name = "Gornji", OrganizationId = OrgId } });
        _uow.Beehives.GetByOrganizationAsync(OrgId).Returns(new[] { new Beehive { Id = 5, Name = "K5", ApiaryId = ApiaryA } });
        _uow.Inspections.FindAsync(Arg.Any<Expression<Func<Inspection, bool>>>()).Returns(Enumerable.Empty<Inspection>());
        _uow.Todos.FindAsync(Arg.Any<Expression<Func<Todo, bool>>>()).Returns(Enumerable.Empty<Todo>());
        _uow.Diets.GetByApiaryIdsAsync(Arg.Any<IEnumerable<int>>()).Returns(Enumerable.Empty<Diet>());
        _uow.ApiaryMoves.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>()).Returns(Enumerable.Empty<ApiaryMove>());
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<HarvestKind>(), Arg.Any<int?>()).Returns(Enumerable.Empty<Harvest>());
        _uow.Harvests.GetSharedAsync(Arg.Any<int?>(), Arg.Any<HarvestKind>(), Arg.Any<int?>()).Returns(Enumerable.Empty<Harvest>());

        var user = new TestCurrentUser { UserId = 1, Role = UserRole.OrganizationAdmin, OrganizationId = OrgId };
        return new StatsService(_uow, user, planLock ?? TestPlanLock.Unlocked());
    }

    private static Harvest Row(int? apiaryId, HiveProductType product, decimal kg, bool perHive = false) => new()
    {
        OrganizationId = OrgId, ApiaryId = apiaryId, Date = DateTime.UtcNow, ProductType = product,
        HoneyType = product == HiveProductType.Honey ? HoneyType.Acacia : null,
        BulkKg = perHive ? null : kg, PricePerKg = 10m,
        Entries = perHive ? [new HarvestEntry { BeehiveId = 5, QuantityKg = kg }] : [],
    };

    [Fact]
    public async Task HoneyFigures_ReadHoneyAlone_IncludingOneFigureAndOrganizationRecords()
    {
        var service = Service();
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), HarvestKind.Honey, Arg.Any<int?>()).Returns(new[]
        {
            Row(ApiaryA, HiveProductType.Honey, 30m, perHive: true),
            Row(ApiaryA, HiveProductType.Honey, 50m),
        });
        _uow.Harvests.GetSharedAsync(OrgId, HarvestKind.Honey, Arg.Any<int?>()).Returns(new[]
        {
            Row(null, HiveProductType.Honey, 20m),
        });
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), HarvestKind.OtherProducts, Arg.Any<int?>()).Returns(new[]
        {
            Row(ApiaryA, HiveProductType.Wax, 7m),
            Row(ApiaryA, HiveProductType.CombHoney, 4m),
        });

        var stats = await service.GetStatsAsync();

        Assert.Equal(100m, stats.SeasonTotalKg);
        Assert.Equal(1000m, stats.EstimatedRevenue);
        Assert.Equal(30m, Assert.Single(stats.TopHivesByYield).Value);          // per-hive lines only
        Assert.Equal(20m, stats.KgByApiary.Single(a => a.Name == "Zajedničko").Value);

        // The "Prinosi" tiles put every product side by side — honey first, comb honey on its own.
        Assert.Equal([HiveProductType.Honey, HiveProductType.CombHoney, HiveProductType.Wax],
            stats.HarvestsByProduct.Select(t => t.ProductType));
        Assert.Equal(100m, stats.HarvestsByProduct[0].Kg);
        Assert.Empty(stats.HiveProductsByPasture);                                            // no moves, no table
    }

    [Fact]
    public async Task OtherProducts_PerHive_SkipLockedHives_AndFollowHiveNumbers()
    {
        var service = Service(TestPlanLock.Locking([], [7]));
        _uow.Beehives.GetByOrganizationAsync(OrgId).Returns(new[]
        {
            new Beehive { Id = 5, Name = "K10", ApiaryId = ApiaryA },
            new Beehive { Id = 6, Name = "K2", ApiaryId = ApiaryA },
            new Beehive { Id = 7, Name = "K3", ApiaryId = ApiaryA },
        });
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), HarvestKind.OtherProducts, Arg.Any<int?>()).Returns(new[]
        {
            new Harvest
            {
                OrganizationId = OrgId, ApiaryId = ApiaryA, Date = DateTime.UtcNow, ProductType = HiveProductType.Propolis,
                Entries =
                [
                    new HarvestEntry { BeehiveId = 5, QuantityKg = 0.05m },
                    new HarvestEntry { BeehiveId = 6, QuantityKg = 0.04m },
                    new HarvestEntry { BeehiveId = 7, QuantityKg = 0.03m },                   // locked by the plan
                ],
            },
        });

        var stats = await service.GetStatsAsync();

        Assert.Equal(["K2", "K10"], stats.HiveProductsByBeehive.Select(r => r.Name));
    }
}
