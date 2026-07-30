using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class FeedbackRepository : Repository<Feedback>, IFeedbackRepository
{
    public FeedbackRepository(MelariumDbContext context) : base(context) { }

    public async Task<IEnumerable<Feedback>> GetByUserAsync(int userId) =>
        await _context.Feedbacks
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ThenByDescending(f => f.Id)
            .ToListAsync();

    public async Task<IEnumerable<Feedback>> GetAllForAdminAsync(FeedbackType? type, FeedbackStatus? status) =>
        await _context.Feedbacks
            .AsNoTracking()
            .Include(f => f.User)
            .Include(f => f.Organization)
            .Where(f => type == null || f.Type == type)
            .Where(f => status == null || f.Status == status)
            .OrderByDescending(f => f.CreatedAt)
            .ThenByDescending(f => f.Id)
            .ToListAsync();

    public async Task<Feedback?> GetByIdWithUserAsync(int id) =>
        await _context.Feedbacks
            .Include(f => f.User)
            .Include(f => f.Organization)
            .Include(f => f.RespondedBy)
            .FirstOrDefaultAsync(f => f.Id == id);

    public async Task<int> CountNewAsync() =>
        await _context.Feedbacks.CountAsync(f => f.Status == FeedbackStatus.New);
}
