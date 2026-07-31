using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Common.Validation;
using Melarium.Application.Features.Notifications;
using Melarium.Application.Features.Profile.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Profile;

public class ProfileService : IProfileService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly ISessionRevoker _sessions;
    private readonly INotificationService _notifications;

    public ProfileService(
        IUnitOfWork uow,
        ICurrentUser currentUser,
        ISessionRevoker sessions,
        INotificationService notifications)
    {
        _uow = uow;
        _currentUser = currentUser;
        _sessions = sessions;
        _notifications = notifications;
    }

    public async Task<ProfileResponseDto> GetProfileAsync()
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException();

        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new NotFoundException(nameof(User), userId);

        return new ProfileResponseDto(user.FirstName, user.LastName, user.Email, user.Phone, user.EmailVerifiedAt);
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

        // Phone uniqueness — blank means "leave the stored number alone", never "clear it", so a
        // client that doesn't send the field can't strip a login identifier off the account.
        // Excluding the caller's own id lets them re-save the profile with their number untouched.
        var newPhone = PhoneRules.Normalize(dto.Phone);
        var phoneChanged = newPhone is not null && newPhone != user.Phone;
        if (phoneChanged && await _uow.Users.IsPhoneTakenAsync(newPhone!, user.Id))
            throw new BusinessRuleException(PhoneRules.DuplicateMessage);

        // Password change (optional)
        var passwordChanged = !string.IsNullOrWhiteSpace(dto.NewPassword);
        if (passwordChanged)
        {
            if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
                throw new BusinessRuleException("Current password is required to set a new password.");

            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
                throw new BusinessRuleException("Current password is incorrect.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);

            // A stolen refresh token stays valid for 14 days — changing the password has to end
            // every session, otherwise "I changed my password" does not actually lock anyone out.
            // This signs the caller out too; the client re-authenticates with the new password.
            await _sessions.RevokeAllAsync(user.Id);
        }

        // Changing the address means the new one is unproven — re-verify before we trust it for
        // things like password recovery.
        var emailChanged = !string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase);
        if (emailChanged)
            user.EmailVerifiedAt = null;

        user.FirstName = dto.FirstName.Trim();
        user.LastName = dto.LastName.Trim();
        user.Email = newEmail;
        if (phoneChanged)
            user.Phone = newPhone;

        await _uow.Users.UpdateAsync(user);
        await _uow.SaveChangesAsync();

        if (passwordChanged)
            await _notifications.NotifyAsync(
                user.Id,
                "Lozinka je promijenjena",
                "Lozinka na vašem računu je upravo promijenjena i odjavljeni ste sa svih uređaja. "
                + "Ako to niste bili vi, odmah zatražite promjenu lozinke putem 'Zaboravili ste lozinku?'.",
                NotificationType.PasswordChanged);

        return new ProfileResponseDto(user.FirstName, user.LastName, user.Email, user.Phone, user.EmailVerifiedAt);
    }
}
