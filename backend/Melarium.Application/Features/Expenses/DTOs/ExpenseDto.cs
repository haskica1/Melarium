using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Expenses.DTOs;

/// <summary>Lightweight expense representation used in list views.</summary>
public class ExpenseDto
{
    public int Id { get; set; }
    public ExpenseSource Source { get; set; }
    public string SourceName { get; set; } = string.Empty;

    /// <summary>Apiary this expense is attributed to; null = shared across the operation (SPEC-25).</summary>
    public int? ApiaryId { get; set; }
    public string? ApiaryName { get; set; }

    public DateTime PurchaseDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "BAM";
    public string? Notes { get; set; }
    public int ItemCount { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}
