using Melarium.Application.Features.Admin.DTOs;

namespace Melarium.Application.Features.Admin;

public interface IAdminService
{
    // Organizations
    Task<IEnumerable<AdminOrganizationDto>> GetAllOrganizationsAsync();
    Task<AdminOrganizationDto> GetOrganizationByIdAsync(int id);
    Task<AdminOrganizationDto> CreateOrganizationAsync(CreateOrganizationDto dto, int? createdById);
    Task<AdminOrganizationDto> UpdateOrganizationAsync(int id, UpdateOrganizationDto dto);
    Task<AdminOrganizationDto> UpdateOrganizationPlanAsync(int id, UpdateOrganizationPlanDto dto);
    Task DeleteOrganizationAsync(int id);

    // Apiaries (for org-scoped picker)
    Task<IEnumerable<AdminApiaryListItemDto>> GetApiariesByOrganizationAsync(int organizationId);

    // Beehives (for org-scoped beehive picker when assigning User role)
    Task<IEnumerable<AdminBeehiveListItemDto>> GetBeehivesByOrganizationAsync(int organizationId);

    // Users
    Task<IEnumerable<AdminUserDto>> GetAllUsersAsync();
    Task<AdminUserDto> GetUserByIdAsync(int id);
    Task<AdminUserDto> CreateUserAsync(CreateAdminUserDto dto);
    Task<AdminUserDto> UpdateUserAsync(int id, UpdateAdminUserDto dto);
    Task DeleteUserAsync(int id);
}
