using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Learning topic (edukacija) data access.</summary>
public interface ILearningTopicRepository : IRepository<LearningTopic>
{
    /// <summary>Published topics, optionally filtered by category and/or current month; newest first.</summary>
    Task<IEnumerable<LearningTopic>> GetPublishedAsync(LearningCategory? category = null, int? month = null);

    /// <summary>A single published topic, or null when missing/unpublished.</summary>
    Task<LearningTopic?> GetPublishedByIdAsync(int id);

    /// <summary>All topics including unpublished — admin listing, newest first.</summary>
    Task<IEnumerable<LearningTopic>> GetAllForAdminAsync();

    /// <summary>Topic ids the user has marked read — one query for the whole list (no N+1).</summary>
    Task<HashSet<int>> GetReadTopicIdsAsync(int userId);

    /// <summary>Whether the user already marked the topic read (idempotence guard).</summary>
    Task<bool> HasReadAsync(int topicId, int userId);

    /// <summary>Stages a read marker; persisted by the caller's SaveChanges.</summary>
    Task AddReadAsync(LearningTopicRead read);
}
