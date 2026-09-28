using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Calendar;
using Melarium.Application.Features.Dashboard;
using Melarium.Application.Features.Todos;
using Melarium.Application.Features.Todos.DTOs;
using Melarium.Application.Features.Weather;
using Melarium.Application.Features.Weather.DTOs;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// The start page (SPEC-29): scoped by the access guard, judged by the same season policy as the
/// alerts — so in winter it says the hives are resting instead of calling them late — and a
/// Beekeeper's yield counts only their own hives.
/// </summary>
public class DashboardServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAccessGuard _access = Substitute.For<IAccessGuard>();
    private readonly ICalendarObligationService _obligations = Substitute.For<ICalendarObligationService>();
    private readonly ITodoService _todos = Substitute.For<ITodoService>();
    private readonly IWeatherService _weather = Substitute.For<IWeatherService>();
    private readonly IConfiguration _config = Substitute.For<IConfiguration>();
    private FakeTime _time = TestSeasons.At(2026, 5, 20);

    private static readonly Apiary Apiary = new() { Id = 1, Name = "Ravan", OrganizationId = 1, Latitude = 43.8, Longitude = 18.4 };

    public DashboardServiceTests()
    {
        _uow.Organizations.GetByIdAsync(1).Returns(new Organization { Id = 1, Name = "Medna dolina" });
        _access.GetAccessibleApiariesAsync(Arg.Any<bool>()).Returns(new List<Apiary> { Apiary });
        _uow.Inspections.GetLastDatesAsync(Arg.Any<IReadOnlyCollection<int>>()).Returns(new Dictionary<int, DateTime>());
        _uow.Inspections.GetLevelsSinceAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateTime>()).Returns(new List<InspectionLevelInfo>());
        _uow.Treatments.GetByApiaryIdsAsync(Arg.Any<IEnumerable<int>>()).Returns(Enumerable.Empty<Treatment>());
        _uow.Diets.GetByApiaryIdsAsync(Arg.Any<IEnumerable<int>>()).Returns(Enumerable.Empty<Diet>());
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<int?>()).Returns(Enumerable.Empty<Harvest>());
        _uow.LearningTopics.GetPublishedAsync(Arg.Any<LearningCategory?>(), Arg.Any<int?>()).Returns(Enumerable.Empty<LearningTopic>());
        _obligations.GatherAsync(Arg.Any<CalendarUserContext>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CalendarCategories>())
            .Returns(new List<CalendarObligation>());
        _todos.GetAllOpenForCurrentUserAsync().Returns(Enumerable.Empty<TodoDto>());
    }

    private DashboardService Service(TestCurrentUser? user = null) =>
        new(_uow, user ?? OrgAdmin, _access, TestPlanLock.Unlocked(), TestSeasons.Calendar(_config),
            TestSeasons.Policy(_config), _obligations, _todos, _weather, _config, _time);

    private static TestCurrentUser OrgAdmin => new() { UserId = 1, Role = UserRole.OrganizationAdmin, OrganizationId = 1 };

    private void GivenHives(params Beehive[] hives) =>
        _access.GetAccessibleBeehivesAsync(Arg.Any<bool>()).Returns(hives.ToList());

    private Beehive Hive(int id, string name, int daysOld) =>
        new() { Id = id, Name = name, ApiaryId = 1, CreatedAt = _time.Now.UtcDateTime.AddDays(-daysOld) };

    [Fact]
    public async Task SystemAdmin_HasNoDashboard()
    {
        var sysAdmin = new TestCurrentUser { UserId = 9, Role = UserRole.SystemAdmin };

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service(sysAdmin).GetAsync());
    }

    [Fact]
    public async Task OverdueHives_BecomeOneAttentionItemPerApiary_AndFillTheStatusChart()
    {
        GivenHives(Hive(11, "K1", 60), Hive(12, "K2", 60), Hive(13, "K3", 2));
        _uow.Inspections.GetLastDatesAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns(new Dictionary<int, DateTime> { [11] = _time.Now.UtcDateTime.AddDays(-30) });

        var dashboard = await Service().GetAsync();

        var item = Assert.Single(dashboard.Attention, a => a.Kind == nameof(NotificationType.InspectionOverdue));
        Assert.Equal("2 košnice bez pregleda", item.Text);
        Assert.Equal(new[] { "K1 · 30 dana", "K2 · još nije pregledana" }, item.Items);

        var status = Assert.Single(dashboard.HiveStatus);
        Assert.Equal((1, 1, 1), (status.InTime, status.Late, status.Never));
        Assert.False(dashboard.HivesResting);
        Assert.Equal(SeasonPhase.MainSeason, dashboard.Season.Phase);
    }

    [Fact]
    public async Task Winter_HivesAreResting_NotLate()
    {
        _time = TestSeasons.At(2027, 1, 12);
        GivenHives(Hive(11, "K1", 300));

        var dashboard = await Service().GetAsync();

        Assert.True(dashboard.HivesResting);
        Assert.DoesNotContain(dashboard.Attention, a => a.Kind == nameof(NotificationType.InspectionOverdue));
        Assert.Equal(SeasonPhase.Winter, dashboard.Season.Phase);
        Assert.Equal(new DateOnly(2027, 2, 16), dashboard.Season.NextStart);
    }

    [Fact]
    public async Task Beekeeper_Yield_CountsOnlyTheirOwnHives()
    {
        GivenHives(Hive(11, "K1", 100));
        var beekeeper = new TestCurrentUser { UserId = 3, Role = UserRole.Beekeeper, OrganizationId = 1 };
        _uow.Harvests.GetByApiariesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<int?>()).Returns(new[]
        {
            new Harvest
            {
                ApiaryId = 1, Date = new DateTime(2026, 5, 10, 9, 0, 0, DateTimeKind.Utc),
                Entries = [new HarvestEntry { BeehiveId = 11, QuantityKg = 12 }, new HarvestEntry { BeehiveId = 99, QuantityKg = 30 }],
            },
        });

        var dashboard = await Service(beekeeper).GetAsync();

        Assert.Equal(12m, dashboard.Counts.YieldThisYearKg);
        Assert.Equal(12m, Assert.Single(dashboard.YieldByMonth).ThisYearKg);
    }

    [Fact]
    public async Task Weather_FlagsASpringFrostAsCritical_ButNotAnOrdinaryWinterOne()
    {
        _weather.GetForecastAsync(43.8, 18.4).Returns(new WeatherForecastDto
        {
            Daily = { new DailyWeatherDto { Date = "2026-04-08", MinTemp = -3, MaxTemp = 9 } },
        });

        _time = TestSeasons.At(2026, 4, 8);
        var spring = Assert.Single(await Service().GetWeatherAsync());
        Assert.Equal("Critical", spring.FrostPriority);
        Assert.Equal(-3, spring.FrostMinTemp);

        _time = TestSeasons.At(2027, 1, 12);
        var winter = Assert.Single(await Service().GetWeatherAsync());
        Assert.Null(winter.FrostPriority);
    }
}
