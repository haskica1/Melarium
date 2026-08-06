using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Repositories;

public class ExpenseRepository : Repository<Expense>, IExpenseRepository
{
    public ExpenseRepository(MelariumDbContext context) : base(context) { }

    public async Task<IEnumerable<Expense>> GetByOrganizationAsync(int organizationId) =>
        await _context.Expenses
            .AsNoTracking()
            .Include(e => e.Items)
            .Include(e => e.CreatedBy)
            .Where(e => e.OrganizationId == organizationId)
            .OrderByDescending(e => e.PurchaseDate)
            .ThenByDescending(e => e.CreatedAt)
            .ToListAsync();

    public async Task<Expense?> GetWithItemsAsync(int id) =>
        await _context.Expenses
            .Include(e => e.Items.OrderBy(i => i.SortOrder))
            .Include(e => e.CreatedBy)
            .FirstOrDefaultAsync(e => e.Id == id);

    public async Task<Dictionary<int, List<(string Currency, decimal Total)>>> GetTotalsByDietsAsync(IEnumerable<int> dietIds)
    {
        var ids = dietIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        // Filtered in SQL; only the per-diet/per-currency grouping runs in memory, over what is
        // realistically a handful of line items per programme.
        var rows = await _context.ExpenseItems
            .AsNoTracking()
            .Where(i => i.DietId != null && ids.Contains(i.DietId!.Value))
            .Select(i => new { DietId = i.DietId!.Value, i.Expense.Currency, i.TotalPrice })
            .ToListAsync();

        return rows
            .GroupBy(r => r.DietId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(r => r.Currency)
                    .Select(cg => (Currency: cg.Key, Total: cg.Sum(r => r.TotalPrice)))
                    .ToList());
    }
}
