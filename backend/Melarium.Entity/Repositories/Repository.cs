using System.Linq.Expressions;
using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Common;
using Melarium.Entity;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

/// <summary>
/// Generic EF Core repository providing common CRUD operations.
/// Concrete repositories extend this and add domain-specific queries.
/// </summary>
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly MelariumDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(MelariumDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<IEnumerable<T>> GetAllAsync() =>
        await _dbSet.AsNoTracking().ToListAsync();

    public async Task<T?> GetByIdAsync(int id) =>
        await _dbSet.FindAsync(id);

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate) =>
        await _dbSet.AsNoTracking().Where(predicate).ToListAsync();

    public async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        return entity;
    }

    public Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(int id) =>
        await _dbSet.AnyAsync(e => e.Id == id);
}
