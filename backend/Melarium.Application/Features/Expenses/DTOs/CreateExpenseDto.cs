using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Expenses.DTOs;

public class CreateExpenseDto
{
    public ExpenseSource Source { get; set; } = ExpenseSource.Manual;

    /// <summary>Optional apiary attribution; null = shared expense (SPEC-25 D1).</summary>
    public int? ApiaryId { get; set; }

    public DateTime PurchaseDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "BAM";
    public string? Notes { get; set; }
    public List<CreateExpenseItemDto> Items { get; set; } = [];
}
