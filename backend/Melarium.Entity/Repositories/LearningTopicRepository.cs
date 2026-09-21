using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class LearningTopicRepository : Repository<LearningTopic>, ILearningTopicRepository
{
    public LearningTopicRepository(MelariumDbContext context) : base(context) { }

    public async Task<IEnumerable<LearningTopic>> GetPublishedAsync(LearningCategory? category = null, int? month = null) =>
        await _context.LearningTopics
            .AsNoTracking()
            .Include(t => t.Author)
            .Where(t => t.IsPublished)
            .Where(t => category == null || t.Category == category)
            .Where(t => month == null || (t.Months != null && t.Months.Contains(month.Value)))
            .OrderByDescending(t => t.PublishedAt)
            .ThenByDescending(t => t.Id)
            .ToListAsync();

    public async Task<LearningTopic?> GetPublishedByIdAsync(int id) =>
        await _context.LearningTopics
            .AsNoTracking()
            .Include(t => t.Author)
            .FirstOrDefaultAsync(t => t.Id == id && t.IsPublished);

    public async Task<IEnumerable<LearningTopic>> GetAllForAdminAsync() =>
        await _context.LearningTopics
            .AsNoTracking()
            .Include(t => t.Author)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<LearningTopic>> GetByAuthorAsync(int authorId) =>
        await _context.LearningTopics
            .AsNoTracking()
            .Where(t => t.AuthorId == authorId)
            .OrderByDescending(t => t.SubmittedAt ?? t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .ToListAsync();

    // Tracked on purpose: the caller edits and saves it.
    public async Task<LearningTopic?> GetOwnSubmissionAsync(int id, int authorId) =>
        await _context.LearningTopics
            .FirstOrDefaultAsync(t => t.Id == id && t.AuthorId == authorId);

    public async Task<int> CountPendingAsync() =>
        await _context.LearningTopics
            .CountAsync(t => t.ReviewStatus == TopicReviewStatus.Pending);

    public async Task<HashSet<int>> GetReadTopicIdsAsync(int userId)
    {
        var ids = await _context.Set<LearningTopicRead>()
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .Select(r => r.TopicId)
            .ToListAsync();
        return [.. ids];
    }

    public async Task<bool> HasReadAsync(int topicId, int userId) =>
        await _context.Set<LearningTopicRead>()
            .AsNoTracking()
            .AnyAsync(r => r.TopicId == topicId && r.UserId == userId);

    public async Task AddReadAsync(LearningTopicRead read) =>
        await _context.Set<LearningTopicRead>().AddAsync(read);
}
