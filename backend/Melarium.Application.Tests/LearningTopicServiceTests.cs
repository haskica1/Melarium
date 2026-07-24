using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Features.Ai;
using Melarium.Application.Features.Learning;
using Melarium.Application.Features.Notifications;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>Learning topics (SPEC-06): published-only visibility, idempotent read tracking, the
/// notify-exactly-once publish rule, and AI-draft parsing (AI never publishes).</summary>
public class LearningTopicServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly IAdvisorAiClient _ai = Substitute.For<IAdvisorAiClient>();

    private LearningTopicService Service(int? userId = 1) =>
        new(_uow, new TestCurrentUser { UserId = userId, Role = UserRole.Beekeeper }, _notifications, _ai);

    private static LearningTopic Topic(int id, bool published = true, DateTime? publishedAt = null, string body = "## Sadržaj") => new()
    {
        Id = id, Title = $"Tema {id}", Category = LearningCategory.SezonskiRadovi,
        Summary = "Sažetak.", BodyMarkdown = body, IsPublished = published, PublishedAt = publishedAt,
    };

    // ── Consumption ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPublished_ForwardsFiltersAndFlagsReadTopics_WithOneReadQuery()
    {
        _uow.LearningTopics.GetPublishedAsync(LearningCategory.Osnove, 7)
            .Returns(new[] { Topic(1), Topic(2) });
        _uow.LearningTopics.GetReadTopicIdsAsync(1).Returns(new HashSet<int> { 2 });

        var result = (await Service().GetPublishedAsync(LearningCategory.Osnove, 7)).ToList();

        await _uow.LearningTopics.Received(1).GetPublishedAsync(LearningCategory.Osnove, 7);
        await _uow.LearningTopics.Received(1).GetReadTopicIdsAsync(1); // grouped, no N+1
        Assert.Equal(2, result.Count);
        Assert.False(result.Single(t => t.Id == 1).IsRead);
        Assert.True(result.Single(t => t.Id == 2).IsRead);
        Assert.Equal("Sezonski radovi", result[0].CategoryName);
    }

    [Fact]
    public async Task GetPublishedById_MissingOrUnpublished_ThrowsNotFound()
    {
        _uow.LearningTopics.GetPublishedByIdAsync(5).Returns((LearningTopic?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => Service().GetPublishedByIdAsync(5));
    }

    [Fact]
    public async Task MarkRead_FirstTime_PersistsMarker()
    {
        _uow.LearningTopics.GetPublishedByIdAsync(3).Returns(Topic(3));
        _uow.LearningTopics.HasReadAsync(3, 1).Returns(false);

        await Service().MarkReadAsync(3);

        await _uow.LearningTopics.Received(1)
            .AddReadAsync(Arg.Is<LearningTopicRead>(r => r.TopicId == 3 && r.UserId == 1));
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task MarkRead_SecondTime_IsIdempotentNoOp()
    {
        _uow.LearningTopics.GetPublishedByIdAsync(3).Returns(Topic(3));
        _uow.LearningTopics.HasReadAsync(3, 1).Returns(true);

        await Service().MarkReadAsync(3);

        await _uow.LearningTopics.DidNotReceive().AddReadAsync(Arg.Any<LearningTopicRead>());
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    // ── Publish toggle ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SetPublished_FirstPublish_NotifiesEveryUserInAppOnly()
    {
        var topic = Topic(4, published: false, publishedAt: null);
        _uow.LearningTopics.GetByIdAsync(4).Returns(topic);
        _uow.Users.GetAllIdsAsync().Returns([10, 11, 12]);

        var dto = await Service().SetPublishedAsync(4, true);

        Assert.True(dto.IsPublished);
        Assert.NotNull(dto.PublishedAt);
        await _notifications.Received(1).NotifyManyInAppAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.Count == 3),
            Arg.Any<string>(),
            Arg.Is<string>(m => m.Contains(topic.Title)),
            NotificationType.LearningTopicPublished,
            4,
            nameof(LearningTopic));
        await _notifications.DidNotReceive().NotifyAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NotificationType>(),
            Arg.Any<int?>(), Arg.Any<string?>()); // no per-user email path
    }

    [Fact]
    public async Task SetPublished_RepublishAfterUnpublish_DoesNotRenotify()
    {
        var topic = Topic(4, published: false, publishedAt: DateTime.UtcNow.AddDays(-7));
        _uow.LearningTopics.GetByIdAsync(4).Returns(topic);

        var dto = await Service().SetPublishedAsync(4, true);

        Assert.True(dto.IsPublished);
        await _notifications.DidNotReceive().NotifyManyInAppAsync(
            Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<NotificationType>(), Arg.Any<int?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task SetPublished_EmptyBody_ThrowsValidationAndSavesNothing()
    {
        _uow.LearningTopics.GetByIdAsync(4).Returns(Topic(4, published: false, body: "   "));

        await Assert.ThrowsAsync<ValidationException>(() => Service().SetPublishedAsync(4, true));
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    // ── AI draft assist ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateDraft_SplitsBodyAndSummaryOnMarker()
    {
        _ai.SendAsync(Arg.Any<IReadOnlyList<ChatMessage>>())
            .Returns("## Uvod\n\nTekst članka.\n\n---SAŽETAK---\nKratki sažetak teme.");

        var draft = await Service().GenerateDraftAsync(new() { Title = "Prihrana" });

        Assert.Equal("## Uvod\n\nTekst članka.", draft.BodyMarkdown);
        Assert.Equal("Kratki sažetak teme.", draft.Summary);
    }

    [Fact]
    public async Task GenerateDraft_AiFailure_ThrowsBusinessRule()
    {
        _ai.SendAsync(Arg.Any<IReadOnlyList<ChatMessage>>())
            .ThrowsAsync(new InvalidOperationException("network"));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service().GenerateDraftAsync(new() { Title = "Prihrana" }));
    }
}
