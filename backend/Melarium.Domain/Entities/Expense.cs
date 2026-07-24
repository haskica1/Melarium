using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

/// <summary>
/// Represents a single beekeeping expense (purchase, supply, etc.) recorded by a member of an organization.
/// An expense contains one or more line items and may originate from manual entry or a scanned receipt.
/// </summary>
public class Expense : BaseEntity
{
    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public int? CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public ExpenseSource Source { get; set; }

    public DateTime PurchaseDate { get; set; }

    /// <summary>The total amount paid, as entered by the user or extracted from the receipt.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>ISO 4217 currency code or local label (e.g. "BAM", "EUR").</summary>
    public string Currency { get; set; } = "BAM";

    public string? Notes { get; set; }

    public List<ExpenseItem> Items { get; set; } = [];
}
