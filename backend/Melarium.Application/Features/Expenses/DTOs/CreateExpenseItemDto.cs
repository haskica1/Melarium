namespace Melarium.Application.Features.Expenses.DTOs;

public class CreateExpenseItemDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public int SortOrder { get; set; }

    /// <summary>Optional attribution to a feeding programme (SPEC-12 Phase E).</summary>
    public int? DietId { get; set; }
}
