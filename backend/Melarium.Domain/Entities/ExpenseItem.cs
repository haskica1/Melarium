using Melarium.Domain.Common;

namespace Melarium.Domain.Entities;

/// <summary>
/// A single line item within an expense (e.g. "Sugar 25 kg @ 1.60/kg = 40 BAM").
/// UnitPrice × Quantity should equal TotalPrice, but the stored TotalPrice is the source of truth
/// to preserve receipt rounding behaviour.
/// </summary>
public class ExpenseItem : BaseEntity
{
    public int ExpenseId { get; set; }
    public Expense Expense { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    /// <summary>Optional unit label: "kg", "pcs", "L", etc.</summary>
    public string? Unit { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TotalPrice { get; set; }

    /// <summary>Display order within the expense (0-based).</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Optional attribution to a feeding programme (SPEC-12 Phase E). SET NULL on delete, never
    /// cascade: an expense is an accounting record of money that actually left the account, and
    /// deleting a feeding programme must never delete it.
    /// </summary>
    public int? DietId { get; set; }
    public Diet? Diet { get; set; }
}
