using Melarium.Application.Features.Learning.DTOs;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Learning;

public interface ILearningTopicService
{
    // ── Consumption (all authenticated roles, published only) ──
    Task<IEnumerable<LearningTopicSummaryDto>> GetPublishedAsync(LearningCategory? category, int? month);
    Task<LearningTopicDetailDto> GetPublishedByIdAsync(int id);

    /// <summary>Marks the topic read for the current user — idempotent.</summary>
    Task MarkReadAsync(int id);

    // ── Authoring (SystemAdmin, role-guarded at the controller) ──
    Task<IEnumerable<AdminLearningTopicDto>> GetAllForAdminAsync();
    Task<AdminLearningTopicDto> GetByIdForAdminAsync(int id);
    Task<AdminLearningTopicDto> CreateAsync(SaveLearningTopicDto dto);
    Task<AdminLearningTopicDto> UpdateAsync(int id, SaveLearningTopicDto dto);
    Task DeleteAsync(int id);

    /// <summary>Publish toggle. The first publish ever notifies all users (in-app only), exactly once.</summary>
    Task<AdminLearningTopicDto> SetPublishedAsync(int id, bool isPublished);

    /// <summary>AI draft assist — returns a draft for the admin to edit; never publishes.</summary>
    Task<LearningDraftDto> GenerateDraftAsync(GenerateDraftDto dto);
}
