using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Models;
using Melarium.Application.Features.Feedbacks;
using Melarium.Application.Features.Feedbacks.DTOs;
using Melarium.Application.Features.Notifications;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// The two subtle behaviours of user feedback (SPEC-13): the deliberate split between the in-app
/// broadcast to every SystemAdmin and the single e-mail to the configured operator address, and the
/// 404-not-403 rule on another user's submission.
/// </summary>
public class FeedbackServiceTests
{
    private const string NotifyAddress = "ops@example.com";

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly IEmailQueue _emailQueue = Substitute.For<IEmailQueue>();
    private readonly IFileStorage _storage = Substitute.For<IFileStorage>();

    private FeedbackService Service(ICurrentUser user, string? notifyEmail = NotifyAddress)
    {
        // Stubbed rather than built from ConfigurationBuilder: the test project intentionally has no
        // Microsoft.Extensions.Configuration reference, and one key does not justify adding one.
        var config = Substitute.For<IConfiguration>();
        config["Feedback:NotifyEmail"].Returns(notifyEmail);

        return new FeedbackService(
            _uow, user, _notifications, _emailQueue, _storage, config,
            NullLogger<FeedbackService>.Instance);
    }

    private static CreateFeedbackDto Dto(FeedbackType type = FeedbackType.Bug) => new(
        type, FeedbackSeverity.High, "  Datum se lomi  ", "  Na iPhoneu datum prelazi preko okvira.  ",
        "/beehives/12", "Mozilla/5.0");

    private static TestCurrentUser Submitter => new()
    {
        UserId = 7, Role = UserRole.Beekeeper, OrganizationId = 3,
    };

    // ── Notification split ─────────────────────────────────────────────────────

    [Fact]
    public async Task Create_NotifiesEverySystemAdminInApp_AndSendsExactlyOneEmail()
    {
        _uow.Users.GetSystemAdminIdsAsync().Returns([1, 2, 3]);

        await Service(Submitter).CreateAsync(Dto());

        // In-app: one broadcast covering all three admins, via the e-mail-free path.
        await _notifications.Received(1).NotifyManyInAppAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.Count == 3),
            Arg.Any<string>(), Arg.Any<string>(),
            NotificationType.FeedbackSubmitted, Arg.Any<int?>(), "Feedback");

        // NotifyAsync would have mailed each admin individually — that is the rejected option.
        await _notifications.DidNotReceive().NotifyAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NotificationType>(),
            Arg.Any<int?>(), Arg.Any<string?>());

        // E-mail: exactly one, to the configured address, regardless of admin count.
        _emailQueue.Received(1).Enqueue(Arg.Is<QueuedEmail>(e =>
            e.ToEmail == NotifyAddress && e.UserId == null));
    }

    [Fact]
    public async Task Create_WithoutConfiguredNotifyEmail_StillSavesAndNotifiesInApp_ButSendsNoEmail()
    {
        _uow.Users.GetSystemAdminIdsAsync().Returns([1]);

        var result = await Service(Submitter, notifyEmail: null).CreateAsync(Dto());

        Assert.Equal("Datum se lomi", result.Subject);
        await _uow.Received().SaveChangesAsync();
        await _notifications.Received(1).NotifyManyInAppAsync(
            Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<string>(), Arg.Any<string>(),
            NotificationType.FeedbackSubmitted, Arg.Any<int?>(), Arg.Any<string?>());
        _emailQueue.DidNotReceive().Enqueue(Arg.Any<QueuedEmail>());
    }

    [Fact]
    public async Task Create_WithNoSystemAdmins_StillSavesTheSubmission()
    {
        _uow.Users.GetSystemAdminIdsAsync().Returns([]);

        await Service(Submitter).CreateAsync(Dto());

        await _uow.Feedbacks.Received(1).AddAsync(Arg.Any<Feedback>());
        await _uow.Received().SaveChangesAsync();
        await _notifications.DidNotReceive().NotifyManyInAppAsync(
            Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<NotificationType>(), Arg.Any<int?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task Create_SnapshotsSubmitterAndOrganisation_AndTrimsInput()
    {
        _uow.Users.GetSystemAdminIdsAsync().Returns([]);
        Feedback? saved = null;
        await _uow.Feedbacks.AddAsync(Arg.Do<Feedback>(f => saved = f));

        await Service(Submitter).CreateAsync(Dto());

        Assert.NotNull(saved);
        Assert.Equal(7, saved!.UserId);
        Assert.Equal(3, saved.OrganizationId);
        Assert.Equal(FeedbackStatus.New, saved.Status);
        Assert.Equal("Datum se lomi", saved.Subject);
        Assert.Equal("Na iPhoneu datum prelazi preko okvira.", saved.Message);
    }

    // ── Ownership: 404, never 403 ──────────────────────────────────────────────

    [Fact]
    public async Task GetMineById_ForAnotherUsersRow_ThrowsNotFound_NotForbidden()
    {
        // A 403 would confirm the row exists, turning by-id reads into an existence oracle.
        _uow.Feedbacks.GetByIdAsync(42).Returns(new Feedback { Id = 42, UserId = 999 });

        await Assert.ThrowsAsync<NotFoundException>(() => Service(Submitter).GetMineByIdAsync(42));
    }

    [Fact]
    public async Task OpenScreenshot_ForAnotherUsersRow_ThrowsNotFound()
    {
        _uow.Feedbacks.GetByIdAsync(42).Returns(new Feedback
        {
            Id = 42, UserId = 999, ScreenshotStoragePath = "2026/07/x.jpg", ScreenshotContentType = "image/jpeg",
        });

        await Assert.ThrowsAsync<NotFoundException>(() => Service(Submitter).OpenScreenshotAsync(42));
        await _storage.DidNotReceive().OpenReadAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task OpenScreenshot_AsSystemAdmin_IsAllowedOnAnyRow()
    {
        var admin = new TestCurrentUser { UserId = 1, Role = UserRole.SystemAdmin };
        _uow.Feedbacks.GetByIdAsync(42).Returns(new Feedback
        {
            Id = 42, UserId = 999, ScreenshotStoragePath = "2026/07/x.jpg", ScreenshotContentType = "image/jpeg",
        });
        _storage.OpenReadAsync("2026/07/x.jpg").Returns(new MemoryStream([1, 2, 3]));

        var (_, contentType) = await Service(admin).OpenScreenshotAsync(42);

        Assert.Equal("image/jpeg", contentType);
    }

    // ── Screenshot rules ───────────────────────────────────────────────────────

    [Fact]
    public async Task AttachScreenshot_WhenOneAlreadyExists_ThrowsBusinessRule()
    {
        _uow.Feedbacks.GetByIdAsync(5).Returns(new Feedback
        {
            Id = 5, UserId = 7, ScreenshotStoragePath = "2026/07/existing.jpg",
        });

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service(Submitter).AttachScreenshotAsync(5, new MemoryStream([0xFF, 0xD8, 0xFF]), 3));
    }

    [Fact]
    public async Task AttachScreenshot_OverSizeCap_ThrowsWithoutTouchingStorage()
    {
        _uow.Feedbacks.GetByIdAsync(5).Returns(new Feedback { Id = 5, UserId = 7 });

        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(Submitter)
            .AttachScreenshotAsync(5, new MemoryStream([0xFF, 0xD8, 0xFF]),
                FeedbackService.MaxScreenshotBytes + 1));

        await _storage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>());
    }

    [Fact]
    public async Task AttachScreenshot_NonImageContent_IsRejectedOnHeaderBytes()
    {
        _uow.Feedbacks.GetByIdAsync(5).Returns(new Feedback { Id = 5, UserId = 7 });

        // A PDF renamed .jpg — the client-supplied type and extension are never trusted.
        var pdf = new MemoryStream("%PDF-1.7 not an image at all"u8.ToArray());

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service(Submitter).AttachScreenshotAsync(5, pdf, pdf.Length));

        await _storage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>());
    }

    [Fact]
    public async Task AttachScreenshot_ValidJpeg_IsStoredWithSniffedContentType()
    {
        var feedback = new Feedback { Id = 5, UserId = 7 };
        _uow.Feedbacks.GetByIdAsync(5).Returns(feedback);
        _storage.SaveAsync(Arg.Any<Stream>(), "image/jpeg").Returns("2026/07/new.jpg");

        var jpeg = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0]);
        var result = await Service(Submitter).AttachScreenshotAsync(5, jpeg, jpeg.Length);

        Assert.True(result.HasScreenshot);
        Assert.Equal("2026/07/new.jpg", feedback.ScreenshotStoragePath);
        Assert.Equal("image/jpeg", feedback.ScreenshotContentType);
    }

    // ── Triage ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_WithReply_NotifiesSubmitterWithBellAndEmail()
    {
        var admin = new TestCurrentUser { UserId = 1, Role = UserRole.SystemAdmin };
        _uow.Feedbacks.GetByIdAsync(5).Returns(new Feedback { Id = 5, UserId = 7, Status = FeedbackStatus.New });
        _uow.Feedbacks.GetByIdWithUserAsync(5).Returns(new Feedback
        {
            Id = 5, UserId = 7, Status = FeedbackStatus.Resolved, AdminResponse = "Popravljeno.",
        });

        await Service(admin).UpdateStatusAsync(5, new UpdateFeedbackStatusDto(FeedbackStatus.Resolved, "Popravljeno."));

        // NotifyAsync here on purpose: someone whose report was answered should get the mail.
        await _notifications.Received(1).NotifyAsync(
            7, Arg.Any<string>(), Arg.Any<string>(),
            NotificationType.FeedbackStatusUpdated, 5, "Feedback");
    }

    [Fact]
    public async Task UpdateStatus_WhenNothingChanged_DoesNotNotify()
    {
        var admin = new TestCurrentUser { UserId = 1, Role = UserRole.SystemAdmin };
        _uow.Feedbacks.GetByIdAsync(5).Returns(new Feedback { Id = 5, UserId = 7, Status = FeedbackStatus.InReview });
        _uow.Feedbacks.GetByIdWithUserAsync(5).Returns(new Feedback { Id = 5, UserId = 7, Status = FeedbackStatus.InReview });

        await Service(admin).UpdateStatusAsync(5, new UpdateFeedbackStatusDto(FeedbackStatus.InReview, null));

        await _notifications.DidNotReceive().NotifyAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NotificationType>(),
            Arg.Any<int?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task UpdateStatus_WhenSubmitterAccountWasDeleted_SkipsNotificationButStillSaves()
    {
        var admin = new TestCurrentUser { UserId = 1, Role = UserRole.SystemAdmin };
        _uow.Feedbacks.GetByIdAsync(5).Returns(new Feedback { Id = 5, UserId = null, Status = FeedbackStatus.New });
        _uow.Feedbacks.GetByIdWithUserAsync(5).Returns(new Feedback { Id = 5, UserId = null, Status = FeedbackStatus.Resolved });

        await Service(admin).UpdateStatusAsync(5, new UpdateFeedbackStatusDto(FeedbackStatus.Resolved, null));

        await _uow.Received().SaveChangesAsync();
        await _notifications.DidNotReceive().NotifyAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NotificationType>(),
            Arg.Any<int?>(), Arg.Any<string?>());
    }
}
