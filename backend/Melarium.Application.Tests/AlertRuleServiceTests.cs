using System.Linq.Expressions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Features.Alerts;
using Melarium.Application.Features.Notifications;
using Melarium.Application.Features.Weather;
using Melarium.Application.Features.Weather.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>Locks each alert rule's trigger condition and its dedup guard (SPEC-04 Part A). The
/// hosting worker stays thin/untested; all logic lives here.</summary>
public class AlertRuleServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly IWeatherService _weather = Substitute.For<IWeatherService>();
    private readonly IConfiguration _config = Substitute.For<IConfiguration>();
    private readonly AlertRuleService _service;

    public AlertRuleServiceTests()
    {
        _service = new AlertRuleService(_uow, _notifications, _weather, _config);

        // Defaults: no queens, a single assigned beekeeper (id 1) as recipient, dedup never hit.
        _uow.Queens.GetActiveByBeehiveIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns(new Dictionary<int, Queen>());
        _uow.Users.GetApiaryAdminIdsAsync(Arg.Any<int>()).Returns(new List<int>());
        _uow.Users.GetOrganizationAdminIdsAsync(Arg.Any<int>()).Returns(new List<int>());
        _uow.Users.GetUserIdsAssignedToBeehiveAsync(Arg.Any<int>()).Returns(new List<int> { 1 });
        _uow.Users.GetUserIdsAssignedToApiaryAsync(Arg.Any<int>()).Returns(new List<int> { 1 });
        _uow.Notifications
            .ExistsRecentAsync(Arg.Any<int>(), Arg.Any<NotificationType>(), Arg.Any<int?>(), Arg.Any<DateTime>())
            .Returns(false);
    }

    private void World(Apiary apiary, Beehive hive, List<Inspection> inspections)
    {
        _uow.Apiaries.GetAllAsync().Returns(new[] { apiary });
        _uow.Beehives.GetByApiaryIdAsync(apiary.Id).Returns(new[] { hive });
        _uow.Inspections.FindAsync(Arg.Any<Expression<Func<Inspection, bool>>>()).Returns(inspections);
    }

    private static Apiary MakeApiary(double? lat = null, double? lon = null) =>
        new() { Id = 1, Name = "Pčelinjak A", OrganizationId = 1, Latitude = lat, Longitude = lon };

    private static Beehive MakeHive(int daysOld) =>
        new() { Id = 10, Name = "K1", ApiaryId = 1, CreatedAt = DateTime.UtcNow.AddDays(-daysOld) };

    [Fact]
    public async Task StaleInspection_Fires_WhenNoInspectionForOverThreshold()
    {
        World(MakeApiary(), MakeHive(daysOld: 30), []);

        await _service.RunDailyScanAsync();

        await _notifications.Received(1).NotifyAsync(1, Arg.Any<string>(), Arg.Any<string>(),
            NotificationType.InspectionOverdue, 10, "Beehive");
    }

    [Fact]
    public async Task StaleInspection_DoesNotFire_WhenRecentDuplicateExists()
    {
        World(MakeApiary(), MakeHive(daysOld: 30), []);
        _uow.Notifications.ExistsRecentAsync(1, NotificationType.InspectionOverdue, 10, Arg.Any<DateTime>())
            .Returns(true);

        await _service.RunDailyScanAsync();

        await _notifications.DidNotReceive().NotifyAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
            NotificationType.InspectionOverdue, Arg.Any<int?>(), Arg.Any<string>());
    }

    [Fact]
    public async Task StaleInspection_DoesNotFire_ForFreshlyCreatedHive()
    {
        World(MakeApiary(), MakeHive(daysOld: 1), []);

        await _service.RunDailyScanAsync();

        await _notifications.DidNotReceive().NotifyAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
            NotificationType.InspectionOverdue, Arg.Any<int?>(), Arg.Any<string>());
    }

    [Fact]
    public async Task HoneyDrop_Fires_WhenLastTwoDecreaseToLow()
    {
        var inspections = new List<Inspection>
        {
            new() { BeehiveId = 10, Date = DateTime.UtcNow.AddDays(-1), HoneyLevel = HoneyLevel.Low },
            new() { BeehiveId = 10, Date = DateTime.UtcNow.AddDays(-8), HoneyLevel = HoneyLevel.High },
        };
        World(MakeApiary(), MakeHive(daysOld: 1), inspections);

        await _service.RunDailyScanAsync();

        await _notifications.Received(1).NotifyAsync(1, Arg.Any<string>(), Arg.Any<string>(),
            NotificationType.HoneyLevelDrop, 10, "Beehive");
    }

    [Fact]
    public async Task HoneyDrop_DoesNotFire_WhenLatestIsNotLow()
    {
        var inspections = new List<Inspection>
        {
            new() { BeehiveId = 10, Date = DateTime.UtcNow.AddDays(-1), HoneyLevel = HoneyLevel.Medium },
            new() { BeehiveId = 10, Date = DateTime.UtcNow.AddDays(-8), HoneyLevel = HoneyLevel.High },
        };
        World(MakeApiary(), MakeHive(daysOld: 1), inspections);

        await _service.RunDailyScanAsync();

        await _notifications.DidNotReceive().NotifyAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
            NotificationType.HoneyLevelDrop, Arg.Any<int?>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Frost_Fires_WhenForecastDipsBelowZero()
    {
        var apiary = MakeApiary(lat: 43.8, lon: 18.4);
        World(apiary, MakeHive(daysOld: 1), []);
        _weather.GetForecastAsync(43.8, 18.4).Returns(new WeatherForecastDto
        {
            Daily = { new DailyWeatherDto { MinTemp = -3, MaxTemp = 5 } },
        });

        await _service.RunDailyScanAsync();

        await _notifications.Received(1).NotifyAsync(1, Arg.Any<string>(), Arg.Any<string>(),
            NotificationType.FrostWarning, 1, "Apiary");
    }

    [Fact]
    public async Task Frost_Skipped_WhenApiaryHasNoCoordinates()
    {
        World(MakeApiary(), MakeHive(daysOld: 1), []);

        await _service.RunDailyScanAsync();

        await _notifications.DidNotReceive().NotifyAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
            NotificationType.FrostWarning, Arg.Any<int?>(), Arg.Any<string>());
        await _weather.DidNotReceive().GetForecastAsync(Arg.Any<double>(), Arg.Any<double>());
    }
}
