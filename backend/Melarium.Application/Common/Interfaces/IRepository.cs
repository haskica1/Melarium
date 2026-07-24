using System.Linq.Expressions;
using Melarium.Domain.Common;

namespace Melarium.Application.Common.Interfaces;

/// <summary>
/// Generic repository interface defining standard data-access operations.
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<IEnumerable<T>> GetAllAsync();
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
    Task<bool> ExistsAsync(int id);
}
