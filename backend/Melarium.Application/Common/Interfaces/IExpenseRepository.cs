using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Expense-specific data access operations.</summary>
public interface IExpenseRepository : IRepository<Expense>
{
    /// <summary>Returns all expenses for an organization, ordered by purchase date descending.</summary>
    Task<IEnumerable<Expense>> GetByOrganizationAsync(int organizationId);

    /// <summary>
    /// Expenses whose <c>PurchaseDate</c> falls inside an inclusive UTC range, items + apiary loaded
    /// (SPEC-25). The boundaries are computed by the caller from local dates — the report's period is
    /// a local-calendar notion, not a UTC one.
    /// </summary>
    Task<IEnumerable<Expense>> GetByOrganizationInRangeAsync(int organizationId, DateTime fromUtc, DateTime toUtc);

    /// <summary>Returns a single expense with its items eagerly loaded.</summary>
    Task<Expense?> GetWithItemsAsync(int id);

    /// <summary>
    /// Sum of attributed ExpenseItem.TotalPrice per diet, grouped by currency (SPEC-12 Phase E).
    /// Grouped rather than summed flat: mixing currencies into one number would be a silent lie, even
    /// though in practice everything is BAM. Diets with nothing attributed are absent from the result.
    /// </summary>
    Task<Dictionary<int, List<(string Currency, decimal Total)>>> GetTotalsByDietsAsync(IEnumerable<int> dietIds);
}
