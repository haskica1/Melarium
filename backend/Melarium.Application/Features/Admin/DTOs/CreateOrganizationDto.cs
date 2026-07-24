namespace Melarium.Application.Features.Admin.DTOs;

public class CreateOrganizationDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
