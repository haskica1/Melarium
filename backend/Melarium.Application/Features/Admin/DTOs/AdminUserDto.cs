namespace Melarium.Application.Features.Admin.DTOs;

public class AdminUserDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>Null for accounts created before phone numbers existed.</summary>
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty;
    public int? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public int? ApiaryId { get; set; }
    public string? ApiaryName { get; set; }
    public List<int> AssignedBeehiveIds { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}
