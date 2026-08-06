using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Expense-specific data access operations.</summary>
public interface IExpenseRepository : IRepository<Expense>
{
    /// <summary>Returns all expenses for an organization, ordered by purchase date descending.</summary>
    Task<IEnumerable<Expense>> GetByOrganizationAsync(int organizationId);

    /// <summary>Returns a single expense with its items eagerly loaded.</summary>
    Task<Expense?> GetWithItemsAsync(int id);

    /// <summary>
    /// Sum of attributed ExpenseItem.TotalPrice per diet, grouped by currency (SPEC-12 Phase E).
    /// Grouped rather than summed flat: mixing currencies into one number would be a silent lie, even
    /// though in practice everything is BAM. Diets with nothing attributed are absent from the result.
    /// </summary>
    Task<Dictionary<int, List<(string Currency, decimal Total)>>> GetTotalsByDietsAsync(IEnumerable<int> dietIds);
}
