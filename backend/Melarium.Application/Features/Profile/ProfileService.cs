using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Features.Profile.DTOs;
using Melarium.Domain.Entities;

namespace Melarium.Application.Features.Profile;

public class ProfileService : IProfileService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public ProfileService(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<ProfileResponseDto> GetProfileAsync()
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException();

        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new NotFoundException(nameof(User), userId);

        return new ProfileResponseDto(user.FirstName, user.LastName, user.Email);
    }

    public async Task<ProfileResponseDto> UpdateProfileAsync(UpdateProfileDto dto)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException();

        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new NotFoundException(nameof(User), userId);

        // Email uniqueness check
        var newEmail = dto.Email.Trim().ToLower();
        if (!string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            var conflict = await _uow.Users.GetByEmailAsync(newEmail);
            if (conflict != null)
                throw new BusinessRuleException($"Email '{dto.Email}' is already in use.");
        }

        // Password change (optional)
        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
                throw new BusinessRuleException("Current password is required to set a new password.");

            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
                throw new BusinessRuleException("Current password is incorrect.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        }

        user.FirstName = dto.FirstName.Trim();
        user.LastName = dto.LastName.Trim();
        user.Email = newEmail;

        await _uow.Users.UpdateAsync(user);
        await _uow.SaveChangesAsync();

        return new ProfileResponseDto(user.FirstName, user.LastName, user.Email);
    }
}
