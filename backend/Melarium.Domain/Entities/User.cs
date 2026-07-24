using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

public class User : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.ApiaryAdmin;

    public int? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    // Apiary assignment — only used for Admin role (apiary-scoped access)
    public int? ApiaryId { get; set; }
    public Apiary? Apiary { get; set; }

    // Beehive assignments — only used for User role (hive-scoped access)
    public ICollection<UserBeehive> AssignedBeehives { get; set; } = new List<UserBeehive>();
}
