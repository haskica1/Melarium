using Melarium.Application.Features.Expenses.DTOs;

namespace Melarium.Application.Features.Expenses;

public interface IExpenseService
{
    Task<IEnumerable<ExpenseDto>> GetAllForCurrentOrganizationAsync();
    Task<ExpenseDetailDto> GetByIdAsync(int id);
    Task<ExpenseDetailDto> CreateAsync(CreateExpenseDto dto);
    Task<ExpenseDetailDto> UpdateAsync(int id, UpdateExpenseDto dto);
    Task DeleteAsync(int id);
}
