namespace Melarium.Application.Features.Admin.DTOs;

public class CreateAdminUserDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "ApiaryAdmin";
    public int? OrganizationId { get; set; }
    public int? ApiaryId { get; set; }
    public List<int> AssignedBeehiveIds { get; set; } = [];
}
