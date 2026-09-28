using Melarium.Application.Common.Email;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Models;
using Melarium.Application.Features.Calendar;
using Melarium.Application.Features.Notifications;
using Melarium.Application.Features.Reminders;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// The 08:00 morning run: the in-app agenda of today's obligations (SPEC-11 Faza A.2) — deduped per
/// day, silent on empty days, respecting the per-user opt-out — and since SPEC-29 the one morning
/// e-mail that carries the scan's Normal alerts together with the agenda.
/// </summary>
public class DailyAgendaServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly ICalendarObligationService _obligations = Substitute.For<ICalendarObligationService>();
    private readonly IEmailQueue _emailQueue = Substitute.For<IEmailQueue>();
    private readonly IConfiguration _config = Substitute.For<IConfiguration>();

    public DailyAgendaServiceTests()
    {
        _uow.NotificationSettings.GetAllAsync().Returns(Enumerable.Empty<NotificationSettings>());
        _uow.Notifications.GetNormalSinceAsync(Arg.Any<DateTime>(), Arg.Any<IReadOnlyCollection<NotificationType>>())
            .Returns(new List<Notification>());
    }

    private DailyAgendaService Service()
    {
        _config["Reminders:DailyAgenda:Enabled"].Returns("true");
        _config["App:TimeZone"].Returns("Europe/Sarajevo");
        return new DailyAgendaService(_uow, _notifications, _obligations, _emailQueue,
            TestSeasons.Policy(_config), _config, TestSeasons.At(2026, 5, 20, hourUtc: 6));
    }

    private static User Beekeeper(int id = 5) => new() { Id = id, Role = UserRole.Beekeeper, OrganizationId = 1 };

    // Apiary-scoped since SPEC-12: a round covers the whole group, so the obligation carries the
    // apiary and a hive count and its BeehiveId is null.
    private static CalendarObligation Feeding() => new(
        ObligationKind.Feeding, "feeding-1", new DateOnly(2026, 5, 20),
        "🍯 Prehrana — Pčelinjak Sjever (3 košnice)", null, "Pčelinjak Sjever", null, 1, false);

    private static Notification Alert(int userId, string title, string message, NotificationType type = NotificationType.InspectionOverdue) =>
        new() { UserId = userId, Title = title, Message = message, Type = type, Priority = NotificationPriority.Normal };

    private void GivenUsers(params User[] users) =>
        _uow.Users.GetAllAsync().Returns(users.AsEnumerable());

    private void GivenSettings(params CalendarSettings[] settings) =>
        _uow.CalendarSettings.GetAllAsync().Returns(settings.AsEnumerable());

    private void GivenObligations(params CalendarObligation[] items) =>
        _obligations.GatherAsync(Arg.Any<CalendarUserContext>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CalendarCategories>())
            .Returns(items.ToList());

    private void GivenAlerts(params Notification[] alerts) =>
        _uow.Notifications.GetNormalSinceAsync(Arg.Any<DateTime>(), Arg.Any<IReadOnlyCollection<NotificationType>>())
            .Returns(alerts.ToList());

    // ── In-app agenda (SPEC-11) ─────────────────────────────────────────────────

    [Fact]
    public async Task Sends_One_Consolidated_Agenda_When_User_Has_Obligations()
    {
        GivenUsers(Beekeeper());
        GivenSettings();
        GivenObligations(Feeding());
        _uow.Notifications.ExistsRecentAsync(Arg.Any<int>(), Arg.Any<NotificationType>(), Arg.Any<int?>(), Arg.Any<DateTime>()).Returns(false);

        await Service().RunAsync();

        await _notifications.Received(1).NotifyAsync(
            5, "Današnje obaveze", Arg.Is<string>(m => m.Contains("Prehrana")),
            NotificationType.DailyAgenda, Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<NotificationPriority?>());
    }

    [Fact]
    public async Task Skips_When_Already_Sent_Today()
    {
        GivenUsers(Beekeeper());
        GivenSettings();
        GivenObligations(Feeding());
        _uow.Notifications.ExistsRecentAsync(Arg.Any<int>(), Arg.Any<NotificationType>(), Arg.Any<int?>(), Arg.Any<DateTime>()).Returns(true);

        await Service().RunAsync();

        await _notifications.DidNotReceive().NotifyAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NotificationType>(),
            Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<NotificationPriority?>());
        _emailQueue.DidNotReceive().Enqueue(Arg.Any<QueuedEmail>());
    }

    [Fact]
    public async Task Silent_When_No_Obligations_And_No_Alerts()
    {
        GivenUsers(Beekeeper());
        GivenSettings();
        GivenObligations(); // empty

        await Service().RunAsync();

        await _notifications.DidNotReceive().NotifyAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NotificationType>(),
            Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<NotificationPriority?>());
        _emailQueue.DidNotReceive().Enqueue(Arg.Any<QueuedEmail>());
    }

    [Fact]
    public async Task Respects_Per_User_Opt_Out()
    {
        GivenUsers(Beekeeper());
        GivenSettings(new CalendarSettings { UserId = 5, DailyAgendaEnabled = false });

        await Service().RunAsync();

        await _obligations.DidNotReceive().GatherAsync(
            Arg.Any<CalendarUserContext>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CalendarCategories>());
        await _notifications.DidNotReceive().NotifyAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NotificationType>(),
            Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<NotificationPriority?>());
    }

    [Fact]
    public async Task Skips_SystemAdmin()
    {
        GivenUsers(new User { Id = 1, Role = UserRole.SystemAdmin });
        GivenSettings();

        await Service().RunAsync();

        await _obligations.DidNotReceive().GatherAsync(
            Arg.Any<CalendarUserContext>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CalendarCategories>());
    }

    // ── The morning e-mail (SPEC-29) ────────────────────────────────────────────

    [Fact]
    public async Task NormalAlertsFromOneScan_GiveOneEmailPerUser_TogetherWithTheAgenda()
    {
        GivenUsers(Beekeeper(5), Beekeeper(6));
        GivenSettings();
        GivenObligations(Feeding());
        GivenAlerts(
            Alert(5, "Košnice bez pregleda", "Pčelinjak 'Sjever' — 3 košnice bez pregleda:\n- K1 (30 dana)\n- K2 (31 dan)\n- K3 (40 dana)"),
            Alert(5, "Opada nivo meda", "Košnici 'K4' opada nivo meda — razmisli o prehrani.", NotificationType.HoneyLevelDrop),
            Alert(5, "Trake za uklanjanje", "Trake su unutra 44 dana.", NotificationType.StripsLeftIn),
            Alert(6, "Hranjenje kasni", "Pčelinjak 'Jug': runda zakazana za 18.05.2026. još nije označena.", NotificationType.FeedingOverdue));
        var sent = new List<QueuedEmail>();
        _emailQueue.Enqueue(Arg.Do<QueuedEmail>(sent.Add));

        await Service().RunAsync();

        Assert.Equal(2, sent.Count);
        var mail = Assert.Single(sent, e => e.UserId == 5).Content;
        Assert.Equal("Jutarnji pregled: 1 obaveza, 3 upozorenja", mail.Subject);

        var cards = mail.Blocks.OfType<EmailCard>().ToList();
        Assert.Equal(new[] { "Košnice bez pregleda", "Opada nivo meda", "Trake za uklanjanje" }, cards.Select(c => c.Title));
        Assert.Contains(cards[0].Rows, r => r.Name == "K2" && r.Detail == "31 dan");
        Assert.Contains(mail.Blocks.OfType<EmailChecklist>().Single().Items, i => i.Name == "Prehrana");
        Assert.Single(sent, e => e.UserId == 6);
    }

    [Fact]
    public async Task AlertsAlone_StillMakeAMorningEmail_WhenTheAgendaIsOff()
    {
        GivenUsers(Beekeeper());
        GivenSettings(new CalendarSettings { UserId = 5, DailyAgendaEnabled = false });
        GivenAlerts(Alert(5, "Košnica bez pregleda", "Košnica 'K1' nije pregledana 30 dana."));

        await Service().RunAsync();

        _emailQueue.Received(1).Enqueue(Arg.Is<QueuedEmail>(e =>
            e.UserId == 5 && e.Content.Subject == "Jutarnji pregled: 1 upozorenje"));
    }

    [Theory]
    [InlineData(EmailNotificationMode.CriticalOnly)]
    [InlineData(EmailNotificationMode.Off)]
    public async Task NoMorningEmail_UnlessTheUserWantsAllEmail(EmailNotificationMode mode)
    {
        GivenUsers(Beekeeper());
        GivenSettings();
        GivenObligations(Feeding());
        GivenAlerts(Alert(5, "Košnica bez pregleda", "Košnica 'K1' nije pregledana 30 dana."));
        _uow.NotificationSettings.GetAllAsync().Returns(new[] { new NotificationSettings { UserId = 5, EmailMode = mode } });

        await Service().RunAsync();

        _emailQueue.DidNotReceive().Enqueue(Arg.Any<QueuedEmail>());
        // The agenda itself still lands in the app.
        await _notifications.Received(1).NotifyAsync(
            5, "Današnje obaveze", Arg.Any<string>(), NotificationType.DailyAgenda,
            Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<NotificationPriority?>());
    }
}
