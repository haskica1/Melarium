namespace Melarium.Application.Features.OrgManagement.DTOs;

/// <summary>
/// Hands the organization over to one of its existing members.
/// </summary>
/// <param name="MemberId">
/// The member who becomes the new OrganizationAdmin. Must already belong to the caller's
/// organization — this creates no accounts and invites nobody.
/// </param>
public record TransferOwnershipDto(int MemberId);
