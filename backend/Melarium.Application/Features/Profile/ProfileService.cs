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

    // ── Account deletion ───────────────────────────────────────────────────────

    public async Task<AccountDeletionPreviewDto> GetDeletionPreviewAsync()
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException();

        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new NotFoundException(nameof(User), userId);

        // A SystemAdmin belongs to no organization, so none of the organization questions apply.
        if (user.OrganizationId is not int organizationId)
            return new AccountDeletionPreviewDto { Mode = ModeAccount };

        var organization = await _uow.Organizations.GetByIdAsync(organizationId)
            ?? throw new NotFoundException(nameof(Organization), organizationId);

        var memberCount = await _uow.Users.CountByOrganizationAsync(organizationId);
        var (mode, _) = ResolveDeletionMode(user, memberCount);

        var preview = new AccountDeletionPreviewDto
        {
            Mode = mode,
            OrganizationName = organization.Name,
            MemberCount = memberCount,
        };

        // The apiary and hive numbers only mean anything when they are about to be destroyed, and
        // counting them costs two queries — so they are looked up only in the case that shows them.
        if (mode == ModeOrganization)
        {
            var apiaries = await _uow.Apiaries.FindAsync(a => a.OrganizationId == organizationId);
            var beehiveCounts = await _uow.Organizations.GetBeehiveCountsAsync(organizationId);

            preview.ApiaryCount = apiaries.Count();
            preview.BeehiveCount = beehiveCounts.GetValueOrDefault(organizationId);
            // Stated flatly rather than after counting rows: an organization with no treatments yet
            // loses nothing, but the sentence the user has to read before agreeing is the same one.
            preview.DeletesTreatmentRegister = true;
        }

        return preview;
    }

    public async Task DeleteMyAccountAsync(DeleteAccountDto dto)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException();

        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new NotFoundException(nameof(User), userId);

        // Re-typed, not taken from the session: an unlocked phone must not be two taps away from
        // destroying the account. Same wording and same cost as the profile's password change.
        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            throw new BusinessRuleException("Lozinka nije tačna.");

        // The platform must not be left without an administrator — the same guard AdminService
        // applies when a SystemAdmin deletes someone else, applied here to deleting yourself.
        if (user.Role == UserRole.SystemAdmin)
        {
            var systemAdmins = await _uow.Users.CountByRoleAsync(UserRole.SystemAdmin);
            if (systemAdmins <= 1)
                throw new BusinessRuleException(
                    "Ne možete obrisati posljednji administratorski račun sistema.");
        }

        Organization? organizationToDelete = null;

        if (user.OrganizationId is int organizationId)
        {
            var memberCount = await _uow.Users.CountByOrganizationAsync(organizationId);
            var (mode, _) = ResolveDeletionMode(user, memberCount);

            if (mode == ModeTransferRequired)
                throw new BusinessRuleException(
                    "Vi ste administrator organizacije koja ima još članova. Prije brisanja računa "
                    + "prenesite vlasništvo nad organizacijom na drugog člana.");

            if (mode == ModeOrganization)
            {
                organizationToDelete = await _uow.Organizations.GetByIdAsync(organizationId)
                    ?? throw new NotFoundException(nameof(Organization), organizationId);

                // Typed by hand, compared exactly (case included): this is the one action in the app
                // that destroys another year's worth of records, so it must not be confirmable by
                // muscle memory. Trimmed only for stray whitespace around the paste.
                if (!string.Equals(
                        dto.OrganizationNameConfirmation?.Trim(),
                        organizationToDelete.Name.Trim(),
                        StringComparison.Ordinal))
                    throw new BusinessRuleException(
                        $"Za potvrdu upišite tačan naziv organizacije: \"{organizationToDelete.Name}\".");
            }
        }

        // `Todo.AssignedToId` is the only foreign key to a user configured with NoAction, so the
        // database refuses the delete instead of clearing it (TodoConfiguration). Releasing the
        // assignments first is what makes deleting an account with open todos possible at all —
        // the same crash AdminService.DeleteUserAsync still has.
        var assignedTodos = await _uow.Todos.FindAsync(t => t.AssignedToId == userId);
        foreach (var todo in assignedTodos)
        {
            todo.AssignedToId = null;
            await _uow.Todos.UpdateAsync(todo);
        }

        // Every other key to a user is Cascade (sessions, notifications, read markers, AI history,
        // hive assignments) or SetNull (everything that records who did the work — feedback,
        // invitations, and the CreatedBy of hives, harvests and treatments). The organization's
        // records therefore survive one member leaving, anonymised, which is what the SetNull
        // configurations were written for.
        await _uow.Users.DeleteAsync(user);

        // Both deletions ride in one SaveChanges, so either the whole thing happens or none of it
        // does. The order is safe without being spelled out: Organization.Users is Restrict, and EF
        // deletes dependents before principals, so the user row goes first and the organization is
        // childless by the time its own delete is sent. Splitting this into two saves could leave a
        // deleted user beside an organization nobody can reach.
        if (organizationToDelete is not null)
            await _uow.Organizations.DeleteAsync(organizationToDelete);

        await _uow.SaveChangesAsync();
    }

    private const string ModeAccount = "account";
    private const string ModeOrganization = "organization";
    private const string ModeTransferRequired = "transfer-required";

    /// <summary>
    /// The one place the three-case rule lives, so the preview and the deletion can never disagree
    /// about what is going to happen.
    ///
    /// Only an OrganizationAdmin takes the organization down with them. A lone member of any other
    /// role — an organization the SystemAdmin set up and still looks after — leaves the organization
    /// standing, because its records are not that member's to erase.
    /// </summary>
    private static (string Mode, int MemberCount) ResolveDeletionMode(User user, int memberCount)
    {
        if (user.Role != UserRole.OrganizationAdmin)
            return (ModeAccount, memberCount);

        return memberCount > 1
            ? (ModeTransferRequired, memberCount)
            : (ModeOrganization, memberCount);
    }
}
