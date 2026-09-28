using System.Linq.Expressions;
using Melarium.Application.Common.Email;
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

/// <summary>
/// Locks each alert rule's trigger condition and its dedup guard (SPEC-04 Part A), and since SPEC-29 the
/// season around them: what winter silences and what it never does, the priority a phase gives, the
/// organization's shift, grouping per apiary, and the once-per-phase notice. The clock is fixed —
/// by default 20 May 2026, main season. The hosting worker stays thin/untested; all logic lives here.
/// </summary>
public class AlertRuleServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly IWeatherService _weather = Substitute.For<IWeatherService>();
    private readonly IConfiguration _config = Substitute.For<IConfiguration>();
    private readonly FakeTime _time = TestSeasons.At(2026, 5, 20);
    private readonly Organization _org = new() { Id = 1, Name = "Medna dolina" };

    public AlertRuleServiceTests()
    {
        // Defaults: no queens, a single assigned beekeeper (id 1) as recipient, dedup never hit.
        _uow.Organizations.GetAllAsync().Returns(new[] { _org });
        _uow.Queens.GetActiveByBeehiveIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns(new Dictionary<int, Queen>());
        _uow.Users.GetApiaryAdminIdsAsync(Arg.Any<int>()).Returns(new List<int>());
        _uow.Users.GetOrganizationAdminIdsAsync(Arg.Any<int>()).Returns(new List<int>());
        _uow.Users.GetUserIdsAssignedToBeehiveAsync(Arg.Any<int>()).Returns(new List<int> { 1 });
        _uow.Users.GetUserIdsAssignedToApiaryAsync(Arg.Any<int>()).Returns(new List<int> { 1 });
        _uow.Users.GetIdsByOrganizationAsync(Arg.Any<int>()).Returns(new List<int>());
        _uow.LearningTopics.GetPublishedAsync(Arg.Any<LearningCategory?>(), Arg.Any<int?>())
            .Returns(Enumerable.Empty<LearningTopic>());
        _uow.Treatments.GetByApiaryAsync(Arg.Any<int>(), Arg.Any<int?>()).Returns(Enumerable.Empty<Treatment>());
        _uow.Diets.GetByApiaryAsync(Arg.Any<int>(), Arg.Any<int?>()).Returns(Enumerable.Empty<Diet>());
        _uow.Notifications
            .ExistsRecentAsync(Arg.Any<int>(), Arg.Any<NotificationType>(), Arg.Any<int?>(), Arg.Any<DateTime>())
            .Returns(false);
    }

    private AlertRuleService Service() =>
        new(_uow, _notifications, _weather, _config, TestPlanLock.Unlocked(),
            TestSeasons.Calendar(_config), TestSeasons.Policy(_config), _time);

    private DateTime Now => _time.Now.UtcDateTime;

    private void World(Apiary apiary, IReadOnlyList<Beehive> hives, List<Inspection> inspections)
    {
        _uow.Apiaries.GetAllAsync().Returns(new[] { apiary });
        _uow.Beehives.GetByApiaryIdAsync(apiary.Id).Returns(hives);
        _uow.Inspections.FindAsync(Arg.Any<Expression<Func<Inspection, bool>>>()).Returns(inspections);
    }

    private void World(Apiary apiary, Beehive hive, List<Inspection> inspections) => World(apiary, [hive], inspections);

    private static Apiary MakeApiary(double? lat = null, double? lon = null) =>
        new() { Id = 1, Name = "Pčelinjak A", OrganizationId = 1, Latitude = lat, Longitude = lon };

    private Beehive MakeHive(int daysOld, int id = 10, string name = "K1") =>
        new() { Id = id, Name = name, ApiaryId = 1, CreatedAt = Now.AddDays(-daysOld) };

    private void GivenForecastMin(double lat, double lon, double min) =>
        _weather.GetForecastAsync(lat, lon).Returns(new WeatherForecastDto
        {
            Daily = { new DailyWeatherDto { MinTemp = min, MaxTemp = min + 8 } },
        });

    // Every argument is a matcher: NotifyAsync has an optional priority, and an assertion that left it
    // out would silently check for "priority == null" only.
    private Task Notified(
        NotificationType type, int? entityId = null, string? entityType = null,
        NotificationPriority? priority = null, int? userId = null, int times = 1, Func<string, bool>? message = null) =>
        _notifications.Received(times).NotifyAsync(
            Arg.Is<int>(u => userId == null || u == userId),
            Arg.Any<string>(),
            Arg.Is<string>(m => message == null || message(m)),
            Arg.Is(type),
            Arg.Is<int?>(e => entityId == null || e == entityId),
            Arg.Is<string?>(t => entityType == null || t == entityType),
            Arg.Is<NotificationPriority?>(p => priority == null || p == priority),
            Arg.Any<EmailContent?>());

    private Task NotNotified(NotificationType type) =>
        _notifications.DidNotReceive().NotifyAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Is(type),
            Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<NotificationPriority?>(), Arg.Any<EmailContent?>());

    // ── Inspection overdue ──────────────────────────────────────────────────────

    [Fact]
    public async Task StaleInspection_Fires_WhenNoInspectionForOverThreshold()
    {
        World(MakeApiary(), MakeHive(daysOld: 30), []);

        await Service().RunDailyScanAsync();

        await Notified(NotificationType.InspectionOverdue, entityId: 1, entityType: "Apiary",
            priority: NotificationPriority.Normal, userId: 1);
    }

    [Fact]
    public async Task StaleInspection_DoesNotFire_WhenRecentDuplicateExists()
    {
        World(MakeApiary(), MakeHive(daysOld: 30), []);
        _uow.Notifications.ExistsRecentAsync(1, NotificationType.InspectionOverdue, 1, Arg.Any<DateTime>())
            .Returns(true);

        await Service().RunDailyScanAsync();

        await NotNotified(NotificationType.InspectionOverdue);
    }

    [Fact]
    public async Task StaleInspection_DoesNotFire_ForFreshlyCreatedHive()
    {
        World(MakeApiary(), MakeHive(daysOld: 1), []);

        await Service().RunDailyScanAsync();

        await NotNotified(NotificationType.InspectionOverdue);
    }

    [Fact]
    public async Task FiveStaleHivesOnOneApiary_GiveOneNotification_ListingThemAll()
    {
        var hives = Enumerable.Range(1, 5).Select(i => MakeHive(daysOld: 40, id: 10 + i, name: $"K{i}")).ToList();
        World(MakeApiary(), hives, []);
        _uow.Users.GetOrganizationAdminIdsAsync(1).Returns(new List<int> { 7 });
        _uow.Users.GetUserIdsAssignedToBeehiveAsync(Arg.Any<int>()).Returns(new List<int>());

        await Service().RunDailyScanAsync();

        await Notified(NotificationType.InspectionOverdue, entityId: 1, entityType: "Apiary", userId: 7,
            message: m => m.Contains("5 košnica") && Enumerable.Range(1, 5).All(i => m.Contains($"- K{i}")));
        await Notified(NotificationType.InspectionOverdue, times: 1);
    }

    [Fact]
    public async Task GroupedNotification_NamesOnlyTheHivesABeekeeperIsAssignedTo()
    {
        var hives = Enumerable.Range(1, 3).Select(i => MakeHive(daysOld: 40, id: 10 + i, name: $"K{i}")).ToList();
        World(MakeApiary(), hives, []);
        _uow.Users.GetOrganizationAdminIdsAsync(1).Returns(new List<int> { 7 });
        _uow.Users.GetUserIdsAssignedToBeehiveAsync(Arg.Any<int>()).Returns(new List<int>());
        _uow.Users.GetUserIdsAssignedToBeehiveAsync(12).Returns(new List<int> { 3 });

        await Service().RunDailyScanAsync();

        await Notified(NotificationType.InspectionOverdue, userId: 7, message: m => m.Contains("3 košnice"));
        await Notified(NotificationType.InspectionOverdue, userId: 3,
            message: m => m.Contains("'K2'") && !m.Contains("K1") && !m.Contains("K3"));
    }

    [Fact]
    public async Task Winter_NoInspectionOverdue()
    {
        _time.Now = new DateTimeOffset(2027, 1, 12, 5, 0, 0, TimeSpan.Zero);
        World(MakeApiary(), MakeHive(daysOld: 120), []);

        await Service().RunDailyScanAsync();

        await NotNotified(NotificationType.InspectionOverdue);
    }

    [Fact]
    public async Task FirstDaysOfSpring_DoNotFlagEveryHive_TheWinterDidNotCount()
    {
        _time.Now = new DateTimeOffset(2026, 2, 20, 5, 0, 0, TimeSpan.Zero);
        var inspections = new List<Inspection> { new() { BeehiveId = 10, Date = new DateTime(2025, 10, 5, 9, 0, 0, DateTimeKind.Utc) } };
        World(MakeApiary(), MakeHive(daysOld: 400), inspections);

        await Service().RunDailyScanAsync();

        await NotNotified(NotificationType.InspectionOverdue);
    }

    [Fact]
    public async Task PhaseBoundary_IsLocal_NotUtc()
    {
        // 23:30 UTC on 14 Nov is already 15 Nov — winter — in Sarajevo: no inspection reminders.
        _time.Now = new DateTimeOffset(2026, 11, 14, 23, 30, 0, TimeSpan.Zero);
        World(MakeApiary(), MakeHive(daysOld: 200), []);

        await Service().RunDailyScanAsync();

        await NotNotified(NotificationType.InspectionOverdue);
    }

    // ── Honey level ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task HoneyDrop_Fires_WhenLastTwoDecreaseToLow()
    {
        var inspections = new List<Inspection>
        {
            new() { BeehiveId = 10, Date = Now.AddDays(-1), HoneyLevel = HoneyLevel.Low },
            new() { BeehiveId = 10, Date = Now.AddDays(-8), HoneyLevel = HoneyLevel.High },
        };
        World(MakeApiary(), MakeHive(daysOld: 1), inspections);

        await Service().RunDailyScanAsync();

        await Notified(NotificationType.HoneyLevelDrop, entityId: 1, entityType: "Apiary", userId: 1);
    }

    [Fact]
    public async Task HoneyDrop_DoesNotFire_WhenLatestIsNotLow()
    {
        var inspections = new List<Inspection>
        {
            new() { BeehiveId = 10, Date = Now.AddDays(-1), HoneyLevel = HoneyLevel.Medium },
            new() { BeehiveId = 10, Date = Now.AddDays(-8), HoneyLevel = HoneyLevel.High },
        };
        World(MakeApiary(), MakeHive(daysOld: 1), inspections);

        await Service().RunDailyScanAsync();

        await NotNotified(NotificationType.HoneyLevelDrop);
    }

    // ── Frost ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Frost_Fires_WhenForecastDipsBelowZero()
    {
        var apiary = MakeApiary(lat: 43.8, lon: 18.4);
        World(apiary, MakeHive(daysOld: 1), []);
        GivenForecastMin(43.8, 18.4, -3);

        await Service().RunDailyScanAsync();

        await Notified(NotificationType.FrostWarning, entityId: 1, entityType: "Apiary", userId: 1);
    }

    [Fact]
    public async Task Frost_Skipped_WhenApiaryHasNoCoordinates()
    {
        World(MakeApiary(), MakeHive(daysOld: 1), []);

        await Service().RunDailyScanAsync();

        await NotNotified(NotificationType.FrostWarning);
        await _weather.DidNotReceive().GetForecastAsync(Arg.Any<double>(), Arg.Any<double>());
    }

    [Fact]
    public async Task Frost_InApril_IsCritical()
    {
        _time.Now = new DateTimeOffset(2026, 4, 8, 5, 0, 0, TimeSpan.Zero);
        World(MakeApiary(lat: 43.8, lon: 18.4), MakeHive(daysOld: 1), []);
        GivenForecastMin(43.8, 18.4, -2);

        await Service().RunDailyScanAsync();

        await Notified(NotificationType.FrostWarning, priority: NotificationPriority.Critical);
    }

    [Fact]
    public async Task Frost_InWinter_IsNotNews()
    {
        _time.Now = new DateTimeOffset(2027, 1, 12, 5, 0, 0, TimeSpan.Zero);
        World(MakeApiary(lat: 43.8, lon: 18.4), MakeHive(daysOld: 1), []);
        GivenForecastMin(43.8, 18.4, -6);

        await Service().RunDailyScanAsync();

        await NotNotified(NotificationType.FrostWarning);
    }

    [Fact]
    public async Task ExtremeCold_InWinter_IsStillSent()
    {
        _time.Now = new DateTimeOffset(2027, 1, 12, 5, 0, 0, TimeSpan.Zero);
        World(MakeApiary(lat: 43.8, lon: 18.4), MakeHive(daysOld: 1), []);
        GivenForecastMin(43.8, 18.4, -18);

        await Service().RunDailyScanAsync();

        await Notified(NotificationType.FrostWarning, priority: NotificationPriority.Normal,
            message: m => m.Contains("ekstremna hladnoća"));
    }

    [Fact]
    public async Task OrganizationShift_MovesTheBoundary_ForTheSameDay()
    {
        // 1 March: spring in the valley (a frost is Critical), still winter at +21 (an ordinary frost is not news).
        _time.Now = new DateTimeOffset(2026, 3, 1, 5, 0, 0, TimeSpan.Zero);
        World(MakeApiary(lat: 43.8, lon: 18.4), MakeHive(daysOld: 1), []);
        GivenForecastMin(43.8, 18.4, -3);

        await Service().RunDailyScanAsync();
        await Notified(NotificationType.FrostWarning, priority: NotificationPriority.Critical);

        _notifications.ClearReceivedCalls();
        _org.SeasonShiftDays = 21;

        await Service().RunDailyScanAsync();
        await NotNotified(NotificationType.FrostWarning);
    }

    // ── Work in progress — never silenced by winter ─────────────────────────────

    [Fact]
    public async Task InWinter_StripsAndKarenca_StillArrive()
    {
        _time.Now = new DateTimeOffset(2027, 1, 12, 5, 0, 0, TimeSpan.Zero);
        World(MakeApiary(), MakeHive(daysOld: 300), []);
        var strips = new Treatment { Id = 31, ApiaryId = 1, Method = ApplicationMethod.Strips, StartDate = Now.AddDays(-50) };
        var karenca = new Treatment
        {
            Id = 32, ApiaryId = 1, Method = ApplicationMethod.Trickling,
            StartDate = Now.AddDays(-20), EndDate = Now.AddDays(-16), WithdrawalDays = 15,
        };
        _uow.Treatments.GetByApiaryAsync(1, Arg.Any<int?>()).Returns(new[] { strips, karenca });

        await Service().RunDailyScanAsync();

        await Notified(NotificationType.StripsLeftIn, entityId: 31, entityType: "Treatment");
        await Notified(NotificationType.KarencaEnded, entityId: 32, entityType: "Treatment");
        await NotNotified(NotificationType.InspectionOverdue);
    }

    // ── Season phase started ────────────────────────────────────────────────────

    [Fact]
    public async Task SeasonPhaseStarted_GoesToEveryMember_WithTheWorkOfThePhase()
    {
        _time.Now = new DateTimeOffset(2026, 10, 3, 5, 0, 0, TimeSpan.Zero); // wintering began 1 Oct
        World(MakeApiary(), MakeHive(daysOld: 1), []);
        _uow.Users.GetIdsByOrganizationAsync(1).Returns(new List<int> { 1, 2 });

        await Service().RunDailyScanAsync();

        await _notifications.Received(1).NotifyAsync(
            1, "Počinje zazimljavanje", Arg.Is<string>(m => m.Contains("Traje od 01.10. do 14.11. —") && m.Contains("- ")),
            NotificationType.SeasonPhaseStarted, Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<NotificationPriority?>());
        await Notified(NotificationType.SeasonPhaseStarted, userId: 2);
    }

    [Fact]
    public async Task SeasonPhaseStarted_IsNotRepeated_WithinThePhase()
    {
        _time.Now = new DateTimeOffset(2026, 10, 5, 5, 0, 0, TimeSpan.Zero);
        World(MakeApiary(), MakeHive(daysOld: 1), []);
        _uow.Users.GetIdsByOrganizationAsync(1).Returns(new List<int> { 1 });
        const int key = 2026 * 10 + (int)SeasonPhase.Wintering;
        _uow.Notifications.ExistsRecentAsync(1, NotificationType.SeasonPhaseStarted, key, Arg.Any<DateTime>())
            .Returns(true);

        await Service().RunDailyScanAsync();

        await NotNotified(NotificationType.SeasonPhaseStarted);
    }

    [Fact]
    public async Task SeasonPhaseStarted_IsNotSentWeeksLate()
    {
        _time.Now = new DateTimeOffset(2026, 9, 26, 5, 0, 0, TimeSpan.Zero); // late summer began 1 Aug
        World(MakeApiary(), MakeHive(daysOld: 1), []);
        _uow.Users.GetIdsByOrganizationAsync(1).Returns(new List<int> { 1 });

        await Service().RunDailyScanAsync();

        await NotNotified(NotificationType.SeasonPhaseStarted);
    }

    // ── Feeding overdue (SPEC-12 Phase D) ───────────────────────────────────────

    private static Diet MakeDiet(int apiaryId, DietStatus status, params FeedingEntry[] entries)
    {
        var diet = new Diet { Id = 5, ApiaryId = apiaryId, Status = status, Name = "Zimska prehrana" };
        diet.Beehives.Add(new DietBeehive { BeehiveId = 10, RemovedOn = null });
        foreach (var e in entries) diet.FeedingEntries.Add(e);
        return diet;
    }

    private FeedingEntry PendingRound(int daysAgo) =>
        new() { ScheduledDate = Now.AddDays(-daysAgo), Status = FeedingEntryStatus.Pending };

    [Fact]
    public async Task FeedingOverdue_Fires_WhenARoundIsPastTheThreshold()
    {
        var apiary = MakeApiary();
        World(apiary, MakeHive(daysOld: 1), []);
        var diet = MakeDiet(apiary.Id, DietStatus.InProgress, PendingRound(daysAgo: 3));
        _uow.Diets.GetByApiaryAsync(apiary.Id).Returns(new[] { diet });

        await Service().RunDailyScanAsync();

        await Notified(NotificationType.FeedingOverdue, entityId: 5, entityType: "Diet", userId: 1);
    }

    [Fact]
    public async Task FeedingOverdue_FiresOncePerDiet_NotOncePerOverdueRound()
    {
        var apiary = MakeApiary();
        World(apiary, MakeHive(daysOld: 1), []);
        // Two rounds are overdue — a programme two weeks behind should still produce one nudge.
        var diet = MakeDiet(apiary.Id, DietStatus.InProgress, PendingRound(daysAgo: 10), PendingRound(daysAgo: 6));
        _uow.Diets.GetByApiaryAsync(apiary.Id).Returns(new[] { diet });

        await Service().RunDailyScanAsync();

        await Notified(NotificationType.FeedingOverdue, entityId: 5, entityType: "Diet", times: 1);
    }

    [Fact]
    public async Task FeedingOverdue_DoesNotFire_WhenProgrammeHasNoActiveHives()
    {
        var apiary = MakeApiary();
        World(apiary, MakeHive(daysOld: 1), []);
        var diet = MakeDiet(apiary.Id, DietStatus.InProgress, PendingRound(daysAgo: 10));
        diet.Beehives.Single().RemovedOn = Now.Date; // no hives left → nothing to do in the field
        _uow.Diets.GetByApiaryAsync(apiary.Id).Returns(new[] { diet });

        await Service().RunDailyScanAsync();

        await NotNotified(NotificationType.FeedingOverdue);
    }

    [Theory]
    [InlineData(DietStatus.NotStarted)]
    [InlineData(DietStatus.Completed)]
    [InlineData(DietStatus.StoppedEarly)]
    public async Task FeedingOverdue_DoesNotFire_ForNonInProgressDiet(DietStatus status)
    {
        var apiary = MakeApiary();
        World(apiary, MakeHive(daysOld: 1), []);
        var diet = MakeDiet(apiary.Id, status, PendingRound(daysAgo: 10));
        _uow.Diets.GetByApiaryAsync(apiary.Id).Returns(new[] { diet });

        await Service().RunDailyScanAsync();

        await NotNotified(NotificationType.FeedingOverdue);
    }

    [Fact]
    public async Task FeedingOverdue_Suppressed_WhenDisabledByConfig()
    {
        _config["Alerts:FeedingOverdue:Enabled"].Returns("false");
        var apiary = MakeApiary();
        World(apiary, MakeHive(daysOld: 1), []);
        var diet = MakeDiet(apiary.Id, DietStatus.InProgress, PendingRound(daysAgo: 10));
        _uow.Diets.GetByApiaryAsync(apiary.Id).Returns(new[] { diet });

        await Service().RunDailyScanAsync();

        await NotNotified(NotificationType.FeedingOverdue);
        await _uow.Diets.DidNotReceive().GetByApiaryAsync(Arg.Any<int>());
    }

    // ── Wording and the e-mail each alert carries (ADR-048) ──────────────────────

    [Fact]
    public async Task FeedingOverdue_Message_DoesNotRepeatItsTitle()
    {
        var apiary = MakeApiary();
        World(apiary, MakeHive(daysOld: 1), []);
        _uow.Diets.GetByApiaryAsync(apiary.Id).Returns(new[] { MakeDiet(apiary.Id, DietStatus.InProgress, PendingRound(daysAgo: 3)) });

        await Service().RunDailyScanAsync();

        // The bell and the morning e-mail put the title first: "Hranjenje kasni: Hranjenje kasni — …" before.
        await Notified(NotificationType.FeedingOverdue,
            message: m => m.StartsWith("Pčelinjak 'Pčelinjak A': runda zakazana za ") && !m.Contains("kasni"));
    }

    [Fact]
    public async Task Frost_Email_ShowsTheForecastAsFacts()
    {
        _time.Now = new DateTimeOffset(2026, 4, 8, 5, 0, 0, TimeSpan.Zero);
        World(MakeApiary(lat: 43.8, lon: 18.4), MakeHive(daysOld: 1), []);
        GivenForecastMin(43.8, 18.4, -2);
        EmailContent? email = null;
        _notifications
            .When(n => n.NotifyAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), NotificationType.FrostWarning,
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<NotificationPriority?>(), Arg.Any<EmailContent?>()))
            .Do(ci => email = ci.ArgAt<EmailContent?>(7));

        await Service().RunDailyScanAsync();

        Assert.NotNull(email);
        Assert.Equal("❄️ Najavljen mraz — Pčelinjak A, −2 °C", email!.Subject);
        Assert.Contains(email.Blocks.OfType<EmailFacts>().Single().Rows,
            f => f.Label == "Najniža temperatura" && f.Value == "−2 °C");
        Assert.Equal("/apiaries/1", email.Button!.Url);
    }

    [Fact]
    public async Task PlanLockPending_SaysTheDateOnce_AndMailsWhatLocks()
    {
        _org.Plan = PlanType.Pro;
        _org.PlanValidUntil = Now.AddDays(1);
        _uow.Users.GetOrganizationAdminIdsAsync(1).Returns(new List<int> { 3 });
        World(MakeApiary(), [], []);
        string? message = null;
        EmailContent? email = null;
        _notifications
            .When(n => n.NotifyAsync(3, Arg.Any<string>(), Arg.Any<string>(), NotificationType.PlanLockPending,
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<NotificationPriority?>(), Arg.Any<EmailContent?>()))
            .Do(ci => { message = ci.ArgAt<string>(2); email = ci.ArgAt<EmailContent?>(7); });

        var service = new AlertRuleService(_uow, _notifications, _weather, _config, TestPlanLock.Locking([1], [10, 11]),
            TestSeasons.Calendar(_config), TestSeasons.Policy(_config), _time);
        await service.RunDailyScanAsync();

        // "ističe 21.05.2026.. Nakon toga" — the date format ends in its own full stop.
        Assert.NotNull(message);
        Assert.DoesNotContain("..", message);
        Assert.Contains(email!.Blocks.OfType<EmailFacts>().Single().Rows,
            f => f.Label == "Postaje nedostupno" && f.Value == "1 pčelinjak i 2 košnice");
        Assert.Equal("/plans", email.Button!.Url);
    }
}
