using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(MelariumDbContext context) : base(context) { }

    public async Task<User?> GetByEmailAsync(string email) =>
        await _context.Users
            .Include(u => u.Organization)
            .Include(u => u.Apiary)
            .FirstOrDefaultAsync(u => u.Email == email.ToLower());

    public async Task<User?> GetByPhoneAsync(string phone) =>
        await _context.Users
            .Include(u => u.Organization)
            .Include(u => u.Apiary)
            .FirstOrDefaultAsync(u => u.Phone == phone);

    public async Task<User?> GetByReferralCodeAsync(string referralCode) =>
        await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.ReferralCode == referralCode);

    public async Task<bool> IsPhoneTakenAsync(string phone, int? excludeUserId = null) =>
        await _context.Users
            .AnyAsync(u => u.Phone == phone && (excludeUserId == null || u.Id != excludeUserId));

    public async Task<IEnumerable<User>> GetAllWithOrganizationAsync() =>
        await _context.Users
            .AsNoTracking()
            .Include(u => u.Organization)
            .Include(u => u.Apiary)
            .Include(u => u.AssignedBeehives).ThenInclude(ub => ub.Beehive)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync();

    public async Task<IEnumerable<User>> GetByOrganizationWithDetailsAsync(int organizationId) =>
        await _context.Users
            .AsNoTracking()
            .Where(u => u.OrganizationId == organizationId)
            .Include(u => u.Organization)
            .Include(u => u.Apiary)
            .Include(u => u.AssignedBeehives).ThenInclude(ub => ub.Beehive)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync();

    public async Task<int> CountByRoleAsync(UserRole role) =>
        await _context.Users.CountAsync(u => u.Role == role);

    public async Task<User?> GetByIdWithOrganizationAsync(int id) =>
        await _context.Users
            .Include(u => u.Organization)
            .Include(u => u.Apiary)
            .FirstOrDefaultAsync(u => u.Id == id);

    public async Task<User?> GetByIdWithAssignedBeehivesAsync(int id) =>
        await _context.Users
            .Include(u => u.Organization)
            .Include(u => u.Apiary)
            .Include(u => u.AssignedBeehives).ThenInclude(ub => ub.Beehive)
            .FirstOrDefaultAsync(u => u.Id == id);

    public async Task<bool> IsUserAssignedToBeehiveAsync(int userId, int beehiveId) =>
        await _context.UserBeehives
            .AnyAsync(ub => ub.UserId == userId && ub.BeehiveId == beehiveId);

    public async Task SetBeehiveAssignmentsAsync(int userId, IEnumerable<int> beehiveIds)
    {
        var existing = await _context.UserBeehives
            .Where(ub => ub.UserId == userId)
            .ToListAsync();

        _context.UserBeehives.RemoveRange(existing);

        foreach (var beehiveId in beehiveIds)
            await _context.UserBeehives.AddAsync(new UserBeehive { UserId = userId, BeehiveId = beehiveId });
    }

    public async Task<HashSet<int>> GetAssignedBeehiveIdsAsync(int userId)
    {
        var ids = await _context.UserBeehives
            .Where(ub => ub.UserId == userId)
            .Select(ub => ub.BeehiveId)
            .ToListAsync();
        return [.. ids];
    }

    public async Task<HashSet<int>> GetAssignedApiaryIdsAsync(int userId)
    {
        var ids = await _context.UserBeehives
            .Where(ub => ub.UserId == userId)
            .Select(ub => ub.Beehive.ApiaryId)
            .Distinct()
            .ToListAsync();
        return [.. ids];
    }

    public async Task<List<int>> GetUserIdsAssignedToBeehiveAsync(int beehiveId) =>
        await _context.UserBeehives
            .Where(ub => ub.BeehiveId == beehiveId)
            .Select(ub => ub.UserId)
            .Distinct()
            .ToListAsync();

    public async Task<List<int>> GetUserIdsAssignedToApiaryAsync(int apiaryId) =>
        await _context.UserBeehives
            .Where(ub => ub.Beehive.ApiaryId == apiaryId)
            .Select(ub => ub.UserId)
            .Distinct()
            .ToListAsync();

    public async Task<List<int>> GetOrganizationAdminIdsAsync(int organizationId) =>
        await _context.Users
            .Where(u => u.Role == UserRole.OrganizationAdmin && u.OrganizationId == organizationId)
            .Select(u => u.Id)
            .ToListAsync();

    public async Task<List<int>> GetApiaryAdminIdsAsync(int apiaryId) =>
        await _context.Users
            .Where(u => u.Role == UserRole.ApiaryAdmin && u.ApiaryId == apiaryId)
            .Select(u => u.Id)
            .ToListAsync();

    public async Task<List<int>> GetAllIdsAsync() =>
        await _context.Users
            .Select(u => u.Id)
            .ToListAsync();

    public async Task<List<int>> GetSystemAdminIdsAsync() =>
        await _context.Users
            .Where(u => u.Role == UserRole.SystemAdmin)
            .Select(u => u.Id)
            .ToListAsync();

    public async Task<int> CountByOrganizationAsync(int organizationId) =>
        await _context.Users.CountAsync(u => u.OrganizationId == organizationId);
}
