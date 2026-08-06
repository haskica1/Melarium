using Melarium.Domain.Common;

namespace Melarium.Domain.Entities;

/// <summary>
/// Links a feeding programme to one beehive it covers. Removal is soft (<see cref="RemovedOn"/>)
/// because hard-deleting the row would erase the fact that the hive was fed at all that month —
/// and it is that history which makes exact consumption per round possible.
/// A hive that was removed can be re-added; that creates a *new* row, which is why the unique
/// index on (DietId, BeehiveId) is filtered on <c>RemovedOn IS NULL</c>.
/// "When the hive joined" is <see cref="BaseEntity.CreatedAt"/> — no extra column.
/// </summary>
public class DietBeehive : BaseEntity
{
    public int DietId { get; set; }
    public Diet Diet { get; set; } = null!;

    public int BeehiveId { get; set; }
    public Beehive Beehive { get; set; } = null!;

    /// <summary>Null while the hive is still on the programme. Set server-side, clamped to today.</summary>
    public DateTime? RemovedOn { get; set; }
}
