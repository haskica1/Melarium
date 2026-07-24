namespace Melarium.Application.Features.Expenses.DTOs;

/// <summary>Full expense representation including all line items.</summary>
public class ExpenseDetailDto : ExpenseDto
{
    public List<ExpenseItemDto> Items { get; set; } = [];
}
