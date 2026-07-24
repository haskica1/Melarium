using AutoMapper;
using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Features.Expenses.DTOs;
using Melarium.Domain.Entities;

namespace Melarium.Application.Features.Expenses;

public class ExpenseService : IExpenseService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ICurrentUser _currentUser;

    public ExpenseService(IUnitOfWork uow, IMapper mapper, ICurrentUser currentUser)
    {
        _uow = uow;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<ExpenseDto>> GetAllForCurrentOrganizationAsync()
    {
        if (_currentUser.OrganizationId is not int orgId)
            return [];

        var expenses = await _uow.Expenses.GetByOrganizationAsync(orgId);
        return _mapper.Map<IEnumerable<ExpenseDto>>(expenses);
    }

    public async Task<ExpenseDetailDto> GetByIdAsync(int id)
    {
        var orgId = RequireOrganization();

        var expense = await _uow.Expenses.GetWithItemsAsync(id)
            ?? throw new NotFoundException(nameof(Expense), id);

        if (expense.OrganizationId != orgId)
            throw new NotFoundException(nameof(Expense), id);

        return _mapper.Map<ExpenseDetailDto>(expense);
    }

    public async Task<ExpenseDetailDto> CreateAsync(CreateExpenseDto dto)
    {
        var orgId = RequireOrganization();

        var expense = _mapper.Map<Expense>(dto);
        expense.OrganizationId = orgId;
        expense.CreatedById = _currentUser.UserId;

        for (int i = 0; i < expense.Items.Count; i++)
            expense.Items[i].SortOrder = i;

        await _uow.Expenses.AddAsync(expense);
        await _uow.SaveChangesAsync();

        var created = await _uow.Expenses.GetWithItemsAsync(expense.Id)
            ?? throw new InvalidOperationException("Expense was not saved correctly.");

        return _mapper.Map<ExpenseDetailDto>(created);
    }

    public async Task<ExpenseDetailDto> UpdateAsync(int id, UpdateExpenseDto dto)
    {
        var orgId = RequireOrganization();

        var expense = await _uow.Expenses.GetWithItemsAsync(id)
            ?? throw new NotFoundException(nameof(Expense), id);

        if (expense.OrganizationId != orgId)
            throw new NotFoundException(nameof(Expense), id);

        // Replace item collection — remove old, add new
        expense.Items.Clear();
        _mapper.Map(dto, expense);

        for (int i = 0; i < expense.Items.Count; i++)
            expense.Items[i].SortOrder = i;

        expense.UpdatedAt = DateTime.UtcNow;

        await _uow.Expenses.UpdateAsync(expense);
        await _uow.SaveChangesAsync();

        var updated = await _uow.Expenses.GetWithItemsAsync(id);
        return _mapper.Map<ExpenseDetailDto>(updated!);
    }

    public async Task DeleteAsync(int id)
    {
        var orgId = RequireOrganization();

        var expense = await _uow.Expenses.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Expense), id);

        if (expense.OrganizationId != orgId)
            throw new NotFoundException(nameof(Expense), id);

        await _uow.Expenses.DeleteAsync(expense);
        await _uow.SaveChangesAsync();
    }

    private int RequireOrganization() =>
        _currentUser.OrganizationId
            ?? throw new ForbiddenAccessException("You must belong to an organization to manage expenses.");
}
