namespace Melarium.Application.Features.Admin.DTOs;

public class UpdateAdminUserDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "ApiaryAdmin";
    public int? OrganizationId { get; set; }
    public int? ApiaryId { get; set; }
    public List<int> AssignedBeehiveIds { get; set; } = [];
}
