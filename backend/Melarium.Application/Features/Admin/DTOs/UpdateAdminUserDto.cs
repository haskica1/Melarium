namespace Melarium.Application.Features.Admin.DTOs;

public class UpdateAdminUserDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>Blank leaves the stored number unchanged — it never clears it.</summary>
    public string? Phone { get; set; }

    public string Role { get; set; } = "ApiaryAdmin";
    public int? OrganizationId { get; set; }
    public int? ApiaryId { get; set; }
    public List<int> AssignedBeehiveIds { get; set; } = [];
}
