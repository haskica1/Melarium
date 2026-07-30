using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Feedback submissions (SPEC-13). All filtering happens in SQL, never in memory.</summary>
public interface IFeedbackRepository : IRepository<Feedback>
{
    /// <summary>The caller's own submissions, newest first.</summary>
    Task<IEnumerable<Feedback>> GetByUserAsync(int userId);

    /// <summary>
    /// Every submission, newest first, with submitter and organisation loaded for the admin list.
    /// Both filters are optional.
    /// </summary>
    Task<IEnumerable<Feedback>> GetAllForAdminAsync(FeedbackType? type, FeedbackStatus? status);

    /// <summary>One submission with submitter/organisation/responder loaded.</summary>
    Task<Feedback?> GetByIdWithUserAsync(int id);

    /// <summary>How many submissions are still <see cref="FeedbackStatus.New"/> — the admin badge count.</summary>
    Task<int> CountNewAsync();
}
