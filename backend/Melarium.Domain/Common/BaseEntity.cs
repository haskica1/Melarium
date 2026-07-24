namespace Melarium.Domain.Common;

/// <summary>
/// Base entity providing a common Id and audit timestamps for all domain entities.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
