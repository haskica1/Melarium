using Melarium.Application.Common.Email;
using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Localization;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Ai;
using Melarium.Application.Features.Learning.DTOs;
using Melarium.Application.Features.Notifications;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Melarium.Application.Features.Learning;

/// <summary>
/// Learning topics (SPEC-06): platform-wide educational content. Consumption endpoints only ever see
/// published topics; authoring is SystemAdmin-only (role guard on the admin controller). The first
/// publish notifies every user in-app exactly once — <see cref="LearningTopic.PublishedAt"/> is the guard.
/// </summary>
public class LearningTopicService : ILearningTopicService
{
    private const string DraftSummaryMarker = "---SAŽETAK---";

    /// <summary>Shortest body a user may submit — an article, not a sentence.</summary>
    private const int SubmissionMinBodyLength = 200;

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly INotificationService _notifications;
    private readonly IProseAiClient _ai;
    private readonly ILogger<LearningTopicService> _logger;

    public LearningTopicService(
        IUnitOfWork uow,
        ICurrentUser currentUser,
        INotificationService notifications,
        IProseAiClient ai,
        ILogger<LearningTopicService> logger)
    {
        _uow           = uow;
        _currentUser   = currentUser;
        _notifications = notifications;
        _ai            = ai;
        _logger        = logger;
    }

    // ── Consumption ──────────────────────────────────────────────────────────────

    public async Task<IEnumerable<LearningTopicSummaryDto>> GetPublishedAsync(LearningCategory? category, int? month)
    {
        var topics = (await _uow.LearningTopics.GetPublishedAsync(category, month)).ToList();
        var readIds = await ReadIdsForCurrentUserAsync();
        return topics.Select(t => ToSummaryDto(t, readIds));
    }

    public async Task<LearningTopicDetailDto> GetPublishedByIdAsync(int id)
    {
        var topic = await _uow.LearningTopics.GetPublishedByIdAsync(id)
            ?? throw new NotFoundException(nameof(LearningTopic), id);

        var readIds = await ReadIdsForCurrentUserAsync();
        var dto = MapCommon(new LearningTopicDetailDto(), topic, readIds);
        dto.BodyMarkdown = topic.BodyMarkdown;
        return dto;
    }

    public async Task MarkReadAsync(int id)
    {
        var topic = await _uow.LearningTopics.GetPublishedByIdAsync(id)
            ?? throw new NotFoundException(nameof(LearningTopic), id);

        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException();

        // Idempotent: double-POST is a no-op (unique (TopicId, UserId) index backs this up).
        if (await _uow.LearningTopics.HasReadAsync(topic.Id, userId)) return;

        await _uow.LearningTopics.AddReadAsync(new LearningTopicRead { TopicId = topic.Id, UserId = userId });
        await _uow.SaveChangesAsync();
    }

    // ── Authoring ────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<AdminLearningTopicDto>> GetAllForAdminAsync() =>
        (await _uow.LearningTopics.GetAllForAdminAsync()).Select(ToAdminDto);

    public async Task<AdminLearningTopicDto> GetByIdForAdminAsync(int id)
    {
        var topic = await _uow.LearningTopics.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(LearningTopic), id);
        return ToAdminDto(topic);
    }

    public async Task<AdminLearningTopicDto> CreateAsync(SaveLearningTopicDto dto)
    {
        var topic = new LearningTopic();
        Apply(topic, dto);

        await _uow.LearningTopics.AddAsync(topic);
        await _uow.SaveChangesAsync();
        return ToAdminDto(topic);
    }

    public async Task<AdminLearningTopicDto> UpdateAsync(int id, SaveLearningTopicDto dto)
    {
        var topic = await _uow.LearningTopics.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(LearningTopic), id);

        Apply(topic, dto);
        topic.UpdatedAt = DateTime.UtcNow;

        await _uow.LearningTopics.UpdateAsync(topic);
        await _uow.SaveChangesAsync();
        return ToAdminDto(topic);
    }

    public async Task DeleteAsync(int id)
    {
        var topic = await _uow.LearningTopics.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(LearningTopic), id);

        await _uow.LearningTopics.DeleteAsync(topic);
        await _uow.SaveChangesAsync();
    }

    public async Task<AdminLearningTopicDto> SetPublishedAsync(int id, bool isPublished)
    {
        var topic = await _uow.LearningTopics.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(LearningTopic), id);

        if (topic.IsPublished == isPublished) return ToAdminDto(topic);

        if (isPublished) RequireBody(topic.BodyMarkdown, "Tema mora imati sadržaj prije objave.");

        var isFirstPublish = false;
        if (isPublished)
        {
            isFirstPublish = MarkPublished(topic);
        }
        else
        {
            topic.IsPublished = false;
            topic.UpdatedAt   = DateTime.UtcNow;
        }

        await _uow.LearningTopics.UpdateAsync(topic);
        await _uow.SaveChangesAsync();

        if (isFirstPublish) await BroadcastFirstPublishAsync(topic);

        return ToAdminDto(topic);
    }

    // ── User submissions (SPEC-26) ─────────────────────────────────────────

    public async Task<IEnumerable<MyLearningSubmissionDto>> GetMySubmissionsAsync()
    {
        var userId = RequireUserId();
        return (await _uow.LearningTopics.GetByAuthorAsync(userId)).Select(ToMineDto);
    }

    public async Task<MyLearningSubmissionDto> GetMySubmissionAsync(int id)
    {
        var userId = RequireUserId();
        var topic  = await _uow.LearningTopics.GetOwnSubmissionAsync(id, userId)
            ?? throw new NotFoundException(nameof(LearningTopic), id);

        return ToMineDto(topic);
    }

    public async Task<MyLearningSubmissionDto> SubmitAsync(SaveLearningTopicDto dto)
    {
        var userId = RequireUserId();
        RequireSubmissionBody(dto.BodyMarkdown);

        var topic = new LearningTopic
        {
            AuthorId     = userId,
            ReviewStatus = TopicReviewStatus.Pending,
            SubmittedAt  = DateTime.UtcNow,
        };
        Apply(topic, dto);

        await _uow.LearningTopics.AddAsync(topic);
        await _uow.SaveChangesAsync();

        await NotifyAdminsOfSubmissionAsync(topic);
        return ToMineDto(topic);
    }

    public async Task<MyLearningSubmissionDto> UpdateMySubmissionAsync(int id, SaveLearningTopicDto dto)
    {
        var userId = RequireUserId();
        var topic  = await _uow.LearningTopics.GetOwnSubmissionAsync(id, userId)
            ?? throw new NotFoundException(nameof(LearningTopic), id);

        if (topic.ReviewStatus == TopicReviewStatus.Approved)
            throw new BusinessRuleException("Odobrena tema je sadržaj platforme i ne može se više mijenjati.");

        RequireSubmissionBody(dto.BodyMarkdown);

        // A rejected topic coming back is a resubmit: the old verdict and its reason stop applying,
        // and the admins have to be told. Editing one that is still queued is just an edit.
        var isResubmit = topic.ReviewStatus == TopicReviewStatus.Rejected;

        Apply(topic, dto);
        topic.ReviewStatus = TopicReviewStatus.Pending;
        topic.SubmittedAt  = DateTime.UtcNow;
        topic.UpdatedAt    = DateTime.UtcNow;
        if (isResubmit)
        {
            topic.RejectionReason = null;
            topic.ReviewedAt      = null;
            topic.ReviewedById    = null;
        }

        await _uow.LearningTopics.UpdateAsync(topic);
        await _uow.SaveChangesAsync();

        if (isResubmit) await NotifyAdminsOfSubmissionAsync(topic, isResubmit: true);
        return ToMineDto(topic);
    }

    public async Task WithdrawMySubmissionAsync(int id)
    {
        var userId = RequireUserId();
        var topic  = await _uow.LearningTopics.GetOwnSubmissionAsync(id, userId)
            ?? throw new NotFoundException(nameof(LearningTopic), id);

        if (topic.ReviewStatus == TopicReviewStatus.Approved)
            throw new BusinessRuleException("Odobrena tema se ne može povući — obratite se administratoru.");

        await _uow.LearningTopics.DeleteAsync(topic);
        await _uow.SaveChangesAsync();
    }

    // ── Review (SystemAdmin) ───────────────────────────────────────────────

    public async Task<AdminLearningTopicDto> ApproveAsync(int id)
    {
        var topic = await RequirePendingAsync(id);
        RequireBody(topic.BodyMarkdown, "Tema mora imati sadržaj prije objave.");

        topic.ReviewStatus = TopicReviewStatus.Approved;
        topic.ReviewedAt   = DateTime.UtcNow;
        topic.ReviewedById = _currentUser.UserId;
        var isFirstPublish = MarkPublished(topic);

        await _uow.LearningTopics.UpdateAsync(topic);
        await _uow.SaveChangesAsync();

        var published = $"Tema \"{topic.Title}\" je odobrena i objavljena u Edukaciji.";
        await NotifyAuthorAsync(topic, "Vaša tema je objavljena", published, TopicEmails.Published(topic, published));

        if (isFirstPublish) await BroadcastFirstPublishAsync(topic);

        return ToAdminDto(topic);
    }

    public async Task<AdminLearningTopicDto> RejectAsync(int id, string reason)
    {
        var topic = await RequirePendingAsync(id);

        topic.ReviewStatus    = TopicReviewStatus.Rejected;
        topic.RejectionReason = reason.Trim();
        topic.ReviewedAt      = DateTime.UtcNow;
        topic.ReviewedById    = _currentUser.UserId;
        topic.UpdatedAt       = DateTime.UtcNow;

        await _uow.LearningTopics.UpdateAsync(topic);
        await _uow.SaveChangesAsync();

        await NotifyAuthorAsync(
            topic,
            "Vaša tema nije objavljena",
            $"Tema \"{topic.Title}\" nije odobrena. Razlog: {topic.RejectionReason} " +
            "Možete je doraditi i poslati ponovo.",
            TopicEmails.Rejected(topic));

        return ToAdminDto(topic);
    }

    public async Task<LearningSubmissionSummaryDto> GetSubmissionSummaryAsync() =>
        new() { PendingCount = await _uow.LearningTopics.CountPendingAsync() };

    private async Task<LearningTopic> RequirePendingAsync(int id)
    {
        var topic = await _uow.LearningTopics.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(LearningTopic), id);

        if (topic.ReviewStatus != TopicReviewStatus.Pending)
            throw new BusinessRuleException("Samo teme koje čekaju odobrenje mogu se odobriti ili odbiti.");

        return topic;
    }

    // ── Publish mechanics (shared by the toggle and by approval) ───────────────────

    /// <summary>Flips visibility on; returns whether this was the topic's first publish ever.</summary>
    private static bool MarkPublished(LearningTopic topic)
    {
        var isFirstPublish = topic.PublishedAt is null;

        topic.IsPublished = true;
        if (isFirstPublish) topic.PublishedAt = DateTime.UtcNow;
        topic.UpdatedAt = DateTime.UtcNow;

        return isFirstPublish;
    }

    /// <summary>
    /// One in-app notification per user, on the very first publish only — a re-publish toggle must
    /// not re-notify. In-app only by design (an email per user per article would be spam). The author
    /// is left out: they already got the personal "odobrena je" message about this same topic.
    /// </summary>
    private async Task BroadcastFirstPublishAsync(LearningTopic topic)
    {
        var userIds = await _uow.Users.GetAllIdsAsync();
        var recipients = topic.AuthorId is int authorId
            ? userIds.Where(uid => uid != authorId).ToList()
            : userIds;

        if (recipients.Count == 0) return;

        await _notifications.NotifyManyInAppAsync(
            recipients,
            "Nova tema u Edukaciji",
            $"Objavljena je nova tema: \"{topic.Title}\".",
            NotificationType.LearningTopicPublished,
            topic.Id,
            nameof(LearningTopic));
    }

    // ── Notifications around review ────────────────────────────────────────

    /// <summary>
    /// In-app bell for every SystemAdmin, no e-mail — the same channel split as feedback, minus the
    /// operator address: a proposed topic is not an incident that has to leave the app. A failure
    /// here must not fail the submission, which is already saved.
    /// </summary>
    private async Task NotifyAdminsOfSubmissionAsync(LearningTopic topic, bool isResubmit = false)
    {
        try
        {
            var adminIds = await _uow.Users.GetSystemAdminIdsAsync();
            if (adminIds.Count == 0) return;

            await _notifications.NotifyManyInAppAsync(
                adminIds,
                isResubmit ? "Tema ponovo poslana na odobrenje" : "Nova tema čeka odobrenje",
                $"\"{topic.Title}\" — {BsLabels.Label(topic.Category)}.",
                NotificationType.LearningTopicSubmitted,
                topic.Id,
                nameof(LearningTopic));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Topic {TopicId} submitted but the admin notification failed", topic.Id);
        }
    }

    /// <summary>
    /// Bell *and* e-mail to the author — here the coupling inside <c>NotifyAsync</c> is what is
    /// wanted: a verdict on something they wrote is worth an inbox. Never fails the review.
    /// </summary>
    private async Task NotifyAuthorAsync(LearningTopic topic, string title, string message, EmailContent email)
    {
        if (topic.AuthorId is not int authorId) return;

        try
        {
            await _notifications.NotifyAsync(
                authorId, title, message,
                NotificationType.LearningTopicReviewed,
                topic.Id,
                nameof(LearningTopic),
                email: email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Topic {TopicId} reviewed but notifying the author failed", topic.Id);
        }
    }

    // ── AI draft assist (Phase 2) ────────────────────────────────────────────────

    public async Task<LearningDraftDto> GenerateDraftAsync(GenerateDraftDto dto)
    {
        var system =
            "Ti si iskusan pčelar i edukator koji piše kratke, praktične edukativne članke za pčelare " +
            "na bosanskom jeziku (region zapadnog Balkana — kontinentalna klima, bagrem/lipa/kesten paše). " +
            "Piši jasno i konkretno, bez fraza. Formatiraj članak u markdownu sa `##` podnaslovima i " +
            "listama gdje pomažu. Ne izmišljaj propise ni brojeve zakona — gdje su propisi relevantni, " +
            "uputi čitaoca da provjeri kod nadležne veterinarske službe. Dužina: 400–700 riječi.\n\n" +
            $"Na kraju odgovora, poslije linije `{DraftSummaryMarker}`, napiši sažetak članka u " +
            "najviše 250 znakova (jedna do dvije rečenice, bez markdowna).";

        var user = string.IsNullOrWhiteSpace(dto.Outline)
            ? $"Napiši članak na temu: \"{dto.Title.Trim()}\"."
            : $"Napiši članak na temu: \"{dto.Title.Trim()}\".\n\nDrži se ovih tačaka:\n{dto.Outline.Trim()}";

        string reply;
        try
        {
            reply = await _ai.SendAsync(new List<ChatMessage>
            {
                new("system", system),
                new("user", user),
            });
        }
        catch (Exception)
        {
            throw new BusinessRuleException("AI servis trenutno nije dostupan. Pokušaj ponovo za koji trenutak.");
        }

        return ParseDraft(reply);
    }

    private static LearningDraftDto ParseDraft(string reply)
    {
        var text = reply.Trim();
        var markerIdx = text.LastIndexOf(DraftSummaryMarker, StringComparison.Ordinal);

        string body, summary;
        if (markerIdx >= 0)
        {
            body    = text[..markerIdx].TrimEnd();
            summary = text[(markerIdx + DraftSummaryMarker.Length)..].Trim();
        }
        else
        {
            // Model ignored the marker — fall back to the first sentence-ish chunk as the teaser.
            body    = text;
            summary = text.Replace("#", "").Replace("*", "").Trim();
        }

        if (summary.Length > 300) summary = summary[..297].TrimEnd() + "…";
        return new LearningDraftDto { BodyMarkdown = body, Summary = summary };
    }

    // ── Helpers & mapping ────────────────────────────────────────────────────────

    private int RequireUserId() => _currentUser.UserId ?? throw new ForbiddenAccessException();

    private static void RequireBody(string? body, string message)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ValidationException(new Dictionary<string, string[]> { ["bodyMarkdown"] = [message] });
    }

    /// <summary>
    /// A proposal has no draft state — it is written before it is sent — so the body rule the admin
    /// form only meets at publish time applies here at submit time, with a floor that keeps
    /// one-liners out of the review queue.
    /// </summary>
    private static void RequireSubmissionBody(string body)
    {
        RequireBody(body, "Tekst teme je obavezan.");

        if (body.Trim().Length < SubmissionMinBodyLength)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["bodyMarkdown"] = [$"Tekst teme mora imati najmanje {SubmissionMinBodyLength} znakova."]
            });
    }

    private static string? AuthorName(LearningTopic t) =>
        t.Author is null ? null : $"{t.Author.FirstName} {t.Author.LastName}".Trim();

    private async Task<HashSet<int>> ReadIdsForCurrentUserAsync() =>
        _currentUser.UserId is int userId
            ? await _uow.LearningTopics.GetReadTopicIdsAsync(userId)
            : [];

    private static void Apply(LearningTopic topic, SaveLearningTopicDto dto)
    {
        topic.Title        = dto.Title.Trim();
        topic.Category     = dto.Category;
        topic.Months       = dto.Months is { Length: > 0 } ? dto.Months.Distinct().OrderBy(m => m).ToArray() : null;
        topic.Summary      = dto.Summary.Trim();
        topic.BodyMarkdown = dto.BodyMarkdown;
        topic.VideoUrl     = string.IsNullOrWhiteSpace(dto.VideoUrl) ? null : dto.VideoUrl.Trim();
        topic.FileUrl      = string.IsNullOrWhiteSpace(dto.FileUrl) ? null : dto.FileUrl.Trim();
        topic.FileName     = string.IsNullOrWhiteSpace(dto.FileName) ? null : dto.FileName.Trim();
    }

    private static T MapCommon<T>(T dto, LearningTopic t, HashSet<int> readIds) where T : LearningTopicSummaryDto
    {
        dto.Id           = t.Id;
        dto.Title        = t.Title;
        dto.Category     = t.Category;
        dto.CategoryName = BsLabels.Label(t.Category);
        dto.Months       = t.Months;
        dto.Summary      = t.Summary;
        dto.VideoUrl     = t.VideoUrl;
        dto.FileUrl      = t.FileUrl;
        dto.FileName     = t.FileName;
        dto.IsRead       = readIds.Contains(t.Id);
        dto.PublishedAt  = t.PublishedAt;
        dto.AuthorName   = AuthorName(t);
        return dto;
    }

    private static LearningTopicSummaryDto ToSummaryDto(LearningTopic t, HashSet<int> readIds) =>
        MapCommon(new LearningTopicSummaryDto(), t, readIds);

    private static AdminLearningTopicDto ToAdminDto(LearningTopic t) => new()
    {
        Id           = t.Id,
        Title        = t.Title,
        Category     = t.Category,
        CategoryName = BsLabels.Label(t.Category),
        Months       = t.Months,
        Summary      = t.Summary,
        BodyMarkdown = t.BodyMarkdown,
        VideoUrl     = t.VideoUrl,
        FileUrl      = t.FileUrl,
        FileName     = t.FileName,
        IsPublished  = t.IsPublished,
        PublishedAt  = t.PublishedAt,

        ReviewStatus     = t.ReviewStatus,
        ReviewStatusName = BsLabels.Label(t.ReviewStatus),
        AuthorId         = t.AuthorId,
        AuthorName       = AuthorName(t),
        SubmittedAt      = t.SubmittedAt,
        ReviewedAt       = t.ReviewedAt,
        RejectionReason  = t.RejectionReason,

        CreatedAt    = t.CreatedAt,
        UpdatedAt    = t.UpdatedAt,
    };

    private static MyLearningSubmissionDto ToMineDto(LearningTopic t) => new()
    {
        Id           = t.Id,
        Title        = t.Title,
        Category     = t.Category,
        CategoryName = BsLabels.Label(t.Category),
        Months       = t.Months,
        Summary      = t.Summary,
        BodyMarkdown = t.BodyMarkdown,
        VideoUrl     = t.VideoUrl,
        FileUrl      = t.FileUrl,
        FileName     = t.FileName,

        ReviewStatus     = t.ReviewStatus,
        ReviewStatusName = BsLabels.Label(t.ReviewStatus),
        SubmittedAt      = t.SubmittedAt,
        ReviewedAt       = t.ReviewedAt,
        RejectionReason  = t.RejectionReason,
        IsPublished      = t.IsPublished,
        CanEdit          = t.ReviewStatus != TopicReviewStatus.Approved,

        CreatedAt    = t.CreatedAt,
        UpdatedAt    = t.UpdatedAt,
    };
}
