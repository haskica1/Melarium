using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Expense-specific data access operations.</summary>
public interface IExpenseRepository : IRepository<Expense>
{
    /// <summary>Returns all expenses for an organization, ordered by purchase date descending.</summary>
    Task<IEnumerable<Expense>> GetByOrganizationAsync(int organizationId);

    /// <summary>Returns a single expense with its items eagerly loaded.</summary>
    Task<Expense?> GetWithItemsAsync(int id);
}
