using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class TodoRepository : Repository<Todo>, ITodoRepository
{
    public TodoRepository(MelariumDbContext context) : base(context) { }

    public async Task<IEnumerable<Todo>> GetByApiaryIdAsync(int apiaryId) =>
        await _context.Todos
            .AsNoTracking()
            .Include(t => t.CreatedBy)
            .Include(t => t.AssignedTo)
            .Where(t => t.ApiaryId == apiaryId)
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => t.DueDate)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Todo>> GetByBeehiveIdAsync(int beehiveId) =>
        await _context.Todos
            .AsNoTracking()
            .Include(t => t.CreatedBy)
            .Include(t => t.AssignedTo)
            .Where(t => t.BeehiveId == beehiveId)
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => t.DueDate)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync();

    public async Task<Todo?> GetByIdWithUsersAsync(int id) =>
        await _context.Todos
            .Include(t => t.CreatedBy)
            .Include(t => t.AssignedTo)
            .FirstOrDefaultAsync(t => t.Id == id);

    public async Task<IEnumerable<Todo>> GetAllOpenByOrganizationAsync(int organizationId) =>
        await _context.Todos
            .AsNoTracking()
            .Include(t => t.Apiary)
            .Include(t => t.Beehive).ThenInclude(b => b!.Apiary)
            .Where(t => !t.IsCompleted && (
                (t.ApiaryId != null && t.Apiary!.OrganizationId == organizationId) ||
                (t.BeehiveId != null && t.Beehive!.Apiary.OrganizationId == organizationId)
            ))
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Todo>> GetAllOpenByApiaryAsync(int apiaryId) =>
        await _context.Todos
            .AsNoTracking()
            .Include(t => t.Beehive)
            .Where(t => !t.IsCompleted && (
                t.ApiaryId == apiaryId ||
                (t.BeehiveId != null && t.Beehive!.ApiaryId == apiaryId)
            ))
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync();
}
