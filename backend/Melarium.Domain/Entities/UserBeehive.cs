namespace Melarium.Domain.Entities;

/// <summary>
/// Many-to-many join between User (role=User) and Beehive.
/// A beekeeper can be assigned to multiple hives; one hive can have multiple beekeepers.
/// </summary>
public class UserBeehive
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int BeehiveId { get; set; }
    public Beehive Beehive { get; set; } = null!;
}
