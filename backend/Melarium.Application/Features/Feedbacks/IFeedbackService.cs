using Melarium.Application.Features.Feedbacks.DTOs;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Feedbacks;

public interface IFeedbackService
{
    // ── Submitter side (any authenticated role) ──
    Task<FeedbackDto> CreateAsync(CreateFeedbackDto dto);
    Task<IEnumerable<FeedbackDto>> GetMineAsync();
    Task<FeedbackDto> GetMineByIdAsync(int id);
    Task<FeedbackDto> AttachScreenshotAsync(int id, Stream content, long sizeBytes);

    /// <summary>Screenshot bytes. Readable by the submitter and by SystemAdmin.</summary>
    Task<(Stream Content, string ContentType)> OpenScreenshotAsync(int id);

    // ── SystemAdmin side ──
    Task<IEnumerable<AdminFeedbackDto>> GetAllForAdminAsync(FeedbackType? type, FeedbackStatus? status);
    Task<AdminFeedbackDto> GetByIdForAdminAsync(int id);
    Task<AdminFeedbackDto> UpdateStatusAsync(int id, UpdateFeedbackStatusDto dto);
    Task<FeedbackSummaryDto> GetSummaryAsync();
    Task DeleteAsync(int id);
}
