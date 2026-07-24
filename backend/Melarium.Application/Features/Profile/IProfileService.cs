using Melarium.Application.Features.Profile.DTOs;

namespace Melarium.Application.Features.Profile;

public interface IProfileService
{
    Task<ProfileResponseDto> GetProfileAsync();
    Task<ProfileResponseDto> UpdateProfileAsync(UpdateProfileDto dto);
}
