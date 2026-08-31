using Melarium.Application.Features.Profile.DTOs;

namespace Melarium.Application.Features.Profile;

public interface IProfileService
{
    Task<ProfileResponseDto> GetProfileAsync();
    Task<ProfileResponseDto> UpdateProfileAsync(UpdateProfileDto dto);

    /// <summary>
    /// What deleting the caller's account would do, so the confirmation screen can ask the right
    /// question. Read-only — it changes nothing and may be called as often as the UI likes.
    /// </summary>
    Task<AccountDeletionPreviewDto> GetDeletionPreviewAsync();

    /// <summary>
    /// Deletes the caller's own account, and — only when they are the last member and the
    /// OrganizationAdmin of their organization — that organization with everything in it.
    /// </summary>
    Task DeleteMyAccountAsync(DeleteAccountDto dto);
}
