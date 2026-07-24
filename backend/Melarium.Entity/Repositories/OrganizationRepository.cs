using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class OrganizationRepository : Repository<Organization>, IOrganizationRepository
{
    public OrganizationRepository(MelariumDbContext context) : base(context) { }

    public async Task<IEnumerable<Organization>> GetAllWithDetailsAsync() =>
        await _context.Organizations
            .AsNoTracking()
            .Include(o => o.Users)
            .Include(o => o.Apiaries)
            .Include(o => o.CreatedBy)
            .OrderBy(o => o.Name)
            .ToListAsync();

    public async Task<Organization?> GetWithDetailsAsync(int id) =>
        await _context.Organizations
            .Include(o => o.Users)
            .Include(o => o.Apiaries)
            .Include(o => o.CreatedBy)
            .FirstOrDefaultAsync(o => o.Id == id);
}
