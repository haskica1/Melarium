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

    // ── User submissions (SPEC-26, any authenticated role) ──

    /// <summary>Everything the caller proposed, in any review state.</summary>
    Task<IEnumerable<MyLearningSubmissionDto>> GetMySubmissionsAsync();

    /// <summary>One of the caller's own proposals; someone else's id is a 404, never a 403.</summary>
    Task<MyLearningSubmissionDto> GetMySubmissionAsync(int id);

    /// <summary>Proposes a topic — created unpublished and Pending, and notifies the SystemAdmins.</summary>
    Task<MyLearningSubmissionDto> SubmitAsync(SaveLearningTopicDto dto);

    /// <summary>
    /// Edits the caller's own proposal. A rejected one goes back to Pending (and re-notifies the
    /// admins); an approved one is platform content and can no longer be touched.
    /// </summary>
    Task<MyLearningSubmissionDto> UpdateMySubmissionAsync(int id, SaveLearningTopicDto dto);

    /// <summary>Withdraws a proposal that has not been approved.</summary>
    Task WithdrawMySubmissionAsync(int id);

    // ── Review (SystemAdmin, role-guarded at the controller) ──

    /// <summary>Approves a pending proposal and publishes it in one step.</summary>
    Task<AdminLearningTopicDto> ApproveAsync(int id);

    /// <summary>Rejects a pending proposal with a reason the author gets verbatim.</summary>
    Task<AdminLearningTopicDto> RejectAsync(int id, string reason);

    /// <summary>Count of proposals waiting for review — the admin nav badge.</summary>
    Task<LearningSubmissionSummaryDto> GetSubmissionSummaryAsync();

    /// <summary>AI draft assist — returns a draft for the admin to edit; never publishes.</summary>
    Task<LearningDraftDto> GenerateDraftAsync(GenerateDraftDto dto);
}
