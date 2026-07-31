namespace Melarium.Application.Features.OrgManagement.DTOs;

public class CreateOrgMemberDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>Required — a second login identifier, stored canonical (see <c>PhoneRules</c>).</summary>
    public string Phone { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
    /// <summary>Must be ApiaryAdmin or Beekeeper.</summary>
    public string Role { get; set; } = "Beekeeper";
    /// <summary>Required when Role is ApiaryAdmin.</summary>
    public int? ApiaryId { get; set; }
    /// <summary>Used when Role is Beekeeper.</summary>
    public List<int> AssignedBeehiveIds { get; set; } = [];
}
