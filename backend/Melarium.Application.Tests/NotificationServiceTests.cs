using Melarium.Application.Common.Email;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Models;
using Melarium.Application.Features.Notifications;
using Melarium.Application.Features.Notifications.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Delivery by priority and the user's settings (SPEC-29): what is stored in the app, what is mailed
/// at once, and what waits for the morning e-mail.
/// </summary>
public class NotificationServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IEmailQueue _emailQueue = Substitute.For<IEmailQueue>();
    private readonly List<Notification> _stored = [];

    public NotificationServiceTests()
    {
        _uow.Notifications.AddAsync(Arg.Do<Notification>(_stored.Add)).Returns(ci => ci.Arg<Notification>());
    }

    private NotificationService Service() => new(_uow, _emailQueue, TestSeasons.Policy());

    private void GivenSettings(EmailNotificationMode mode, bool normalInApp = true, bool infoInApp = true) =>
        _uow.NotificationSettings.GetByUserIdAsync(1).Returns(new NotificationSettings
        {
            UserId = 1, EmailMode = mode, NormalAlertsInApp = normalInApp, InfoAlertsInApp = infoInApp,
        });

    [Fact]
    public async Task NormalAlert_IsStored_ButWaitsForTheMorningEmail()
    {
        await Service().NotifyAsync(1, "Košnica bez pregleda", "…", NotificationType.InspectionOverdue,
            1, "Apiary", NotificationPriority.Normal);

        Assert.Single(_stored);
        Assert.Equal(NotificationPriority.Normal, _stored[0].Priority);
        _emailQueue.DidNotReceive().Enqueue(Arg.Any<QueuedEmail>());
    }

    [Fact]
    public async Task CriticalAlert_IsMailedAtOnce()
    {
        await Service().NotifyAsync(1, "Najavljen mraz", "…", NotificationType.FrostWarning,
            1, "Apiary", NotificationPriority.Critical);

        Assert.Equal(NotificationPriority.Critical, _stored.Single().Priority);
        _emailQueue.Received(1).Enqueue(Arg.Is<QueuedEmail>(e => e.UserId == 1));
    }

    [Fact]
    public async Task HumanTriggeredNews_StillGoesAtOnce_ByDefault()
    {
        await Service().NotifyAsync(1, "Novi zadatak", "…", NotificationType.TodoCreated);

        _emailQueue.Received(1).Enqueue(Arg.Any<QueuedEmail>());
    }

    [Fact]
    public async Task CriticalOnly_SilencesTheRest()
    {
        GivenSettings(EmailNotificationMode.CriticalOnly);

        await Service().NotifyAsync(1, "Novi zadatak", "…", NotificationType.TodoCreated);

        Assert.Single(_stored);
        _emailQueue.DidNotReceive().Enqueue(Arg.Any<QueuedEmail>());
    }

    [Fact]
    public async Task EmailOff_KeepsCriticalInTheApp_WithoutMail()
    {
        GivenSettings(EmailNotificationMode.Off);

        await Service().NotifyAsync(1, "Najavljen mraz", "…", NotificationType.FrostWarning,
            1, "Apiary", NotificationPriority.Critical);

        Assert.Single(_stored);
        _emailQueue.DidNotReceive().Enqueue(Arg.Any<QueuedEmail>());
    }

    [Fact]
    public async Task Security_IsMailed_EvenWithEmailOff()
    {
        GivenSettings(EmailNotificationMode.Off);

        await Service().NotifyAsync(1, "Lozinka promijenjena", "…", NotificationType.PasswordChanged);

        _emailQueue.Received(1).Enqueue(Arg.Any<QueuedEmail>());
    }

    [Fact]
    public async Task HiddenAlertCategory_IsNotCreatedAtAll_ButEventsAre()
    {
        GivenSettings(EmailNotificationMode.All, normalInApp: false, infoInApp: false);
        var service = Service();

        await service.NotifyAsync(1, "Košnica bez pregleda", "…", NotificationType.InspectionOverdue,
            1, "Apiary", NotificationPriority.Normal);
        await service.NotifyAsync(1, "Stara matica", "…", NotificationType.OldQueen,
            1, "Apiary", NotificationPriority.Info);
        await service.NotifyAsync(1, "Novi zadatak", "…", NotificationType.TodoCreated);

        Assert.Equal(NotificationType.TodoCreated, _stored.Single().Type);
    }

    [Fact]
    public void Fit_CutsAnOverlongMessage_AtALineBreak()
    {
        var message = string.Join("\n", Enumerable.Range(1, 200).Select(i => $"- Košnica broj {i} (nikad)"));

        var fitted = NotificationService.Fit(message);

        Assert.True(fitted.Length <= NotificationService.MaxMessageLength);
        Assert.EndsWith("\n…", fitted);
        Assert.DoesNotContain("(nik\n", fitted);
    }

    [Fact]
    public async Task Settings_DefaultToAllEmail_UntilSaved_ThenAreUpdatedInPlace()
    {
        var service = Service();
        Assert.Equal(new NotificationSettingsDto(EmailNotificationMode.All, true, true), await service.GetSettingsAsync(1));

        var saved = await service.UpdateSettingsAsync(1,
            new UpdateNotificationSettingsDto(EmailNotificationMode.CriticalOnly, false, true));

        Assert.Equal(EmailNotificationMode.CriticalOnly, saved.EmailMode);
        await _uow.NotificationSettings.Received(1).AddAsync(Arg.Is<NotificationSettings>(s =>
            s.UserId == 1 && s.EmailMode == EmailNotificationMode.CriticalOnly && !s.NormalAlertsInApp));
    }

    // ── What the mail shows (ADR-048) ───────────────────────────────────────────

    [Fact]
    public async Task MailedNotification_GetsItsTypesIconAndAButtonToWhatItIsAbout()
    {
        QueuedEmail? sent = null;
        _emailQueue.Enqueue(Arg.Do<QueuedEmail>(e => sent = e));

        await Service().NotifyAsync(1, "Košnica dodijeljena", "Dodijeljeni ste košnici 'K7'.",
            NotificationType.BeehiveAssigned, 7, "Beehive");

        Assert.NotNull(sent);
        Assert.Equal("🐝", sent!.Content.Icon);
        Assert.Equal("Dodjela", sent.Content.Eyebrow);
        Assert.Equal("/beehives/7", sent.Content.Button!.Url);
        Assert.Equal(NotificationEmail.NowReason, sent.Content.Reason);
        Assert.True(sent.Content.LinkSettings);
    }

    [Fact]
    public async Task SendersContent_IsMailed_ButTheFooterIsThePolicys()
    {
        QueuedEmail? sent = null;
        _emailQueue.Enqueue(Arg.Do<QueuedEmail>(e => sent = e));
        var custom = new EmailContent("Lozinka je promijenjena") { Blocks = [new EmailText("…")] };

        await Service().NotifyAsync(1, "Lozinka je promijenjena", "…", NotificationType.PasswordChanged, email: custom);

        // Security: always mailed, red, and no settings link — the settings cannot switch it off.
        Assert.Same(custom.Blocks, sent!.Content.Blocks);
        Assert.Equal(EmailTone.Critical, sent.Content.Tone);
        Assert.Equal(NotificationEmail.SecurityReason, sent.Content.Reason);
        Assert.False(sent.Content.LinkSettings);
    }
}
