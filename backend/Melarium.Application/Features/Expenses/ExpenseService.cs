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
        await EnsureItemDietsAttributableAsync(orgId, dto.Items);

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
        await EnsureItemDietsAttributableAsync(orgId, dto.Items);

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

    /// <summary>
    /// Every attributed dietId must belong to a diet whose apiary is in the caller's own
    /// organization. Expense is organization-scoped while Diet is apiary-scoped, so this is the
    /// boundary check between the two. A cross-organization id is well-formed but simply not
    /// attributable — that is a <see cref="Melarium.Application.Common.Exceptions.ValidationException"/>
    /// (400), not a 404 (the diet does exist) and not a 403 (this is a data-shape rule, not a
    /// per-apiary permission the way managing a diet directly is).
    /// </summary>
    private async Task EnsureItemDietsAttributableAsync(int orgId, IEnumerable<CreateExpenseItemDto> items)
    {
        var dietIds = items.Where(i => i.DietId.HasValue).Select(i => i.DietId!.Value).Distinct().ToList();
        if (dietIds.Count == 0) return;

        foreach (var dietId in dietIds)
        {
            var diet = await _uow.Diets.GetByIdAsync(dietId)
                ?? throw new NotFoundException(nameof(Diet), dietId);

            var apiary = await _uow.Apiaries.GetByIdAsync(diet.ApiaryId);
            if (apiary is null || apiary.OrganizationId != orgId)
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    ["items"] = [$"Program prehrane {dietId} ne pripada vašoj organizaciji."]
                });
        }
    }
}
