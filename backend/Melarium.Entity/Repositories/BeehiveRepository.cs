using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class BeehiveRepository : Repository<Beehive>, IBeehiveRepository
{
    public BeehiveRepository(MelariumDbContext context) : base(context) { }

    public async Task<Beehive?> GetWithInspectionsAsync(int id) =>
        await _context.Beehives
            .Include(b => b.Inspections.OrderByDescending(i => i.Date))
            .Include(b => b.CreatedBy)
            .FirstOrDefaultAsync(b => b.Id == id);

    public async Task<IEnumerable<Beehive>> GetByApiaryIdAsync(int apiaryId) =>
        await _context.Beehives
            .AsNoTracking()
            .Include(b => b.CreatedBy)
            .Where(b => b.ApiaryId == apiaryId)
            .OrderBy(b => b.Name)
            .ToListAsync();

    public async Task<IEnumerable<Beehive>> GetByOrganizationAsync(int organizationId) =>
        await _context.Beehives
            .AsNoTracking()
            .Include(b => b.Apiary)
            .Where(b => b.Apiary.OrganizationId == organizationId)
            .OrderBy(b => b.Apiary.Name)
            .ThenBy(b => b.Name)
            .ToListAsync();

    public async Task<Beehive?> GetByUniqueIdAsync(Guid uniqueId) =>
        await _context.Beehives
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.UniqueId == uniqueId);

    public async Task<IEnumerable<Beehive>> GetAllWithUniqueIdAsync() =>
        await _context.Beehives
            .Where(b => b.UniqueId != null)
            .ToListAsync();

    public async Task<int> CountByOrganizationAsync(int organizationId) =>
        await _context.Beehives.CountAsync(b => b.Apiary.OrganizationId == organizationId);
}
