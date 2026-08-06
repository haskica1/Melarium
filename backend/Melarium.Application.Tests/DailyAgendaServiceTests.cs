using Melarium.Application.Common.Interfaces;
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
/// The daily 08:00 agenda (SPEC-11 Faza A.2): one consolidated notification per user with today's
/// obligations, deduped per day, silent on empty days, and respecting the per-user opt-out.
/// </summary>
public class DailyAgendaServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly ICalendarObligationService _obligations = Substitute.For<ICalendarObligationService>();
    private readonly IConfiguration _config = Substitute.For<IConfiguration>();

    private DailyAgendaService Service()
    {
        _config["Reminders:DailyAgenda:Enabled"].Returns("true");
        _config["App:TimeZone"].Returns("Europe/Sarajevo");
        return new DailyAgendaService(_uow, _notifications, _obligations, _config);
    }

    private static User Beekeeper(int id = 5) => new() { Id = id, Role = UserRole.Beekeeper, OrganizationId = 1 };

    // Apiary-scoped since SPEC-12: a round covers the whole group, so the obligation carries the
    // apiary and a hive count and its BeehiveId is null.
    private static CalendarObligation Feeding() => new(
        ObligationKind.Feeding, "feeding-1", DateOnly.FromDateTime(DateTime.UtcNow),
        "🍯 Prehrana — Pčelinjak Sjever (3 košnice)", null, "Pčelinjak Sjever", null, 1, false);

    private void GivenUsers(params User[] users) =>
        _uow.Users.GetAllAsync().Returns(users.AsEnumerable());

    private void GivenSettings(params CalendarSettings[] settings) =>
        _uow.CalendarSettings.GetAllAsync().Returns(settings.AsEnumerable());

    private void GivenObligations(params CalendarObligation[] items) =>
        _obligations.GatherAsync(Arg.Any<CalendarUserContext>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CalendarCategories>())
            .Returns(items.ToList());

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
            NotificationType.DailyAgenda, Arg.Any<int?>(), Arg.Any<string?>());
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
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NotificationType>(), Arg.Any<int?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task Silent_When_No_Obligations_Today()
    {
        GivenUsers(Beekeeper());
        GivenSettings();
        GivenObligations(); // empty

        await Service().RunAsync();

        await _notifications.DidNotReceive().NotifyAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NotificationType>(), Arg.Any<int?>(), Arg.Any<string?>());
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
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NotificationType>(), Arg.Any<int?>(), Arg.Any<string?>());
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
}
