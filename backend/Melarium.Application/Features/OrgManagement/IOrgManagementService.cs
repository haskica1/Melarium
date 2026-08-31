using Melarium.Application.Features.OrgManagement.DTOs;

namespace Melarium.Application.Features.OrgManagement;

public interface IOrgManagementService
{
    /// <summary>Returns all manageable members in the caller's organization (User and Admin roles only).</summary>
    Task<IEnumerable<OrgMemberDto>> GetMembersAsync();

    /// <summary>Returns a single member of the caller's organization with their current assignments.</summary>
    Task<OrgMemberDto> GetMemberAsync(int memberId);

    /// <summary>
    /// Updates beehive assignments for a User-role member.
    /// ApiaryAdmin callers are restricted to beehives within their own apiary.
    /// </summary>
    Task<OrgMemberDto> UpdateBeehiveAssignmentsAsync(int memberId, UpdateBeehiveAssignmentsDto dto);

    /// <summary>Updates the apiary assignment for an Admin-role member. OrgAdmin only.</summary>
    Task<OrgMemberDto> UpdateApiaryAssignmentAsync(int memberId, UpdateApiaryAssignmentDto dto);

    /// <summary>
    /// Returns beehives available for assignment.
    /// OrgAdmin gets all org beehives; ApiaryAdmin gets only their apiary's beehives.
    /// </summary>
    Task<IEnumerable<OrgAvailableBeehiveDto>> GetAvailableBeehivesAsync();

    /// <summary>Returns all apiaries in the caller's organization for assigning to Admin users. OrgAdmin only.</summary>
    Task<IEnumerable<OrgAvailableApiaryDto>> GetAvailableApiariesAsync();

    /// <summary>
    /// Creates a new ApiaryAdmin or Beekeeper member in the caller's organization. OrgAdmin only.
    /// </summary>
    Task<OrgMemberDto> CreateMemberAsync(CreateOrgMemberDto dto);

    /// <summary>
    /// Hands the organization over to an existing member: they become the OrganizationAdmin and the
    /// caller steps down to Beekeeper. OrgAdmin only.
    /// </summary>
    /// <remarks>
    /// Returns nothing on purpose. Role is a JWT claim, so both accounts have their sessions
    /// revoked and the caller is signed out — there is no screen left for a response to fill.
    /// </remarks>
    Task TransferOwnershipAsync(TransferOwnershipDto dto);
}
