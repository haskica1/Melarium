namespace Melarium.Application.Features.Expenses.DTOs;

public class UpdateExpenseDto
{
    public DateTime PurchaseDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "BAM";
    public string? Notes { get; set; }
    public List<CreateExpenseItemDto> Items { get; set; } = [];
}
