namespace Melarium.Application.Features.Expenses.DTOs;

public class ExpenseItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public int SortOrder { get; set; }
}
