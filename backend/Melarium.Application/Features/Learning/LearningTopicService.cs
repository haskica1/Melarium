using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Localization;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Ai;
using Melarium.Application.Features.Learning.DTOs;
using Melarium.Application.Features.Notifications;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Learning;

/// <summary>
/// Learning topics (SPEC-06): platform-wide educational content. Consumption endpoints only ever see
/// published topics; authoring is SystemAdmin-only (role guard on the admin controller). The first
/// publish notifies every user in-app exactly once — <see cref="LearningTopic.PublishedAt"/> is the guard.
/// </summary>
public class LearningTopicService : ILearningTopicService
{
    private const string DraftSummaryMarker = "---SAŽETAK---";

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly INotificationService _notifications;
    private readonly IProseAiClient _ai;

    public LearningTopicService(
        IUnitOfWork uow,
        ICurrentUser currentUser,
        INotificationService notifications,
        IProseAiClient ai)
    {
        _uow           = uow;
        _currentUser   = currentUser;
        _notifications = notifications;
        _ai            = ai;
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

        if (isPublished && string.IsNullOrWhiteSpace(topic.BodyMarkdown))
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["bodyMarkdown"] = ["Tema mora imati sadržaj prije objave."]
            });

        var isFirstPublish = isPublished && topic.PublishedAt is null;

        topic.IsPublished = isPublished;
        if (isFirstPublish) topic.PublishedAt = DateTime.UtcNow;
        topic.UpdatedAt = DateTime.UtcNow;

        await _uow.LearningTopics.UpdateAsync(topic);
        await _uow.SaveChangesAsync();

        // Broadcast only on the very first publish — a re-publish toggle must not re-notify.
        // In-app only by design (an email per user per article would be spam).
        if (isFirstPublish)
        {
            var userIds = await _uow.Users.GetAllIdsAsync();
            await _notifications.NotifyManyInAppAsync(
                userIds,
                "Nova tema u Edukaciji",
                $"Objavljena je nova tema: \"{topic.Title}\".",
                NotificationType.LearningTopicPublished,
                topic.Id,
                nameof(LearningTopic));
        }

        return ToAdminDto(topic);
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
        CreatedAt    = t.CreatedAt,
        UpdatedAt    = t.UpdatedAt,
    };
}
