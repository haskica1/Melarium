using Melarium.Domain.Common;

namespace Melarium.Domain.Entities;

/// <summary>
/// One field-level correction to a <see cref="Queen"/> record (SPEC-03). Written by
/// <c>QueenService.UpdateAsync</c> whenever an edit changes a value, so mistakes made
/// entering the initial data can be traced later — who changed what, and from what to what.
/// </summary>
public class QueenEditLog : BaseEntity
{
    public int QueenId { get; set; }
    public Queen Queen { get; set; } = null!;

    public int? EditedById { get; set; }
    public User? EditedBy { get; set; }

    /// <summary>Bosnian display label for the changed field, e.g. "Godište", "Napomene".</summary>
    public string FieldLabel { get; set; } = string.Empty;

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
