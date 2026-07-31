using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Common.Validation;
using Melarium.Application.Features.Notifications;
using Melarium.Application.Features.OrgManagement.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.OrgManagement;

public class OrgManagementService : IOrgManagementService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifications;
    private readonly ICurrentUser _currentUser;
    private readonly IPlanGuard _plan;
    private readonly ISessionRevoker _sessions;

    public OrgManagementService(
        IUnitOfWork uow,
        INotificationService notifications,
        ICurrentUser currentUser,
        IPlanGuard plan,
        ISessionRevoker sessions)
    {
        _uow           = uow;
        _notifications = notifications;
        _currentUser   = currentUser;
        _plan          = plan;
        _sessions      = sessions;
    }

    public async Task<IEnumerable<OrgMemberDto>> GetMembersAsync()
    {
        if (_currentUser.OrganizationId is not int orgId)
            return [];

        // Scoped in SQL. This used to load every user on the platform and filter in memory, so an
        // organisation's member list got slower as *other* tenants signed up.
        var users = await _uow.Users.GetByOrganizationWithDetailsAsync(orgId);
        return users
            // OrganizationAdmin included so co-admins of the org are visible too (read-only here —
            // GetMemberAsync below still refuses to open the assignments screen for them, since an
            // org admin has nothing to assign).
            .Where(u => u.Role is UserRole.Beekeeper or UserRole.ApiaryAdmin or UserRole.OrganizationAdmin)
            .Select(MapMember)
            .ToList();
    }

    public async Task<OrgMemberDto> GetMemberAsync(int memberId)
    {
        var orgId = RequireOrganization();

        var user = await _uow.Users.GetByIdWithAssignedBeehivesAsync(memberId)
            ?? throw new NotFoundException(nameof(User), memberId);

        if (user.OrganizationId != orgId)
            throw new ForbiddenAccessException("This member does not belong to your organization.");

        if (user.Role is not (UserRole.Beekeeper or UserRole.ApiaryAdmin))
            throw new BusinessRuleException("Only User and Admin role members can be managed here.");

        return MapMember(user);
    }

    public async Task<OrgMemberDto> UpdateBeehiveAssignmentsAsync(int memberId, UpdateBeehiveAssignmentsDto dto)
    {
        var orgId = RequireOrganization();

        var member = await _uow.Users.GetByIdWithAssignedBeehivesAsync(memberId)
            ?? throw new NotFoundException(nameof(User), memberId);

        if (member.OrganizationId != orgId)
            throw new ForbiddenAccessException("This member does not belong to your organization.");

        if (member.Role != UserRole.Beekeeper)
            throw new BusinessRuleException("Beehive assignments can only be set for User-role members.");

        // Validate each requested beehive belongs to the org (and to caller's apiary if ApiaryAdmin)
        foreach (var beehiveId in dto.BeehiveIds)
        {
            var beehive = await _uow.Beehives.GetByIdAsync(beehiveId)
                ?? throw new NotFoundException(nameof(Beehive), beehiveId);

            var apiary = await _uow.Apiaries.GetByIdAsync(beehive.ApiaryId)
                ?? throw new NotFoundException(nameof(Apiary), beehive.ApiaryId);

            if (apiary.OrganizationId != orgId)
                throw new BusinessRuleException($"Beehive '{beehive.Name}' does not belong to your organization.");

            if (_currentUser.Role == UserRole.ApiaryAdmin && _currentUser.ApiaryId is int callerApiaryId
                && beehive.ApiaryId != callerApiaryId)
                throw new BusinessRuleException($"Beehive '{beehive.Name}' is not in your apiary.");
        }

        // Capture old assignments for change detection
        var oldBeehiveIds = member.AssignedBeehives.Select(ub => ub.BeehiveId).ToHashSet();
        var newBeehiveIds = dto.BeehiveIds.ToHashSet();

        await _uow.Users.SetBeehiveAssignmentsAsync(memberId, dto.BeehiveIds);
        await _uow.SaveChangesAsync();

        // Notify member of beehive assignment changes
        foreach (var beehiveId in newBeehiveIds.Except(oldBeehiveIds))
        {
            var beehive = await _uow.Beehives.GetByIdAsync(beehiveId);
            if (beehive == null) continue;
            await _notifications.NotifyAsync(
                memberId,
                "Košnica dodijeljena",
                $"Dodijeljeni ste košnici '{beehive.Name}'.",
                NotificationType.BeehiveAssigned,
                beehiveId, nameof(Beehive));
        }

        foreach (var beehiveId in oldBeehiveIds.Except(newBeehiveIds))
        {
            var beehive = await _uow.Beehives.GetByIdAsync(beehiveId);
            await _notifications.NotifyAsync(
                memberId,
                "Košnica uklonjena",
                $"Uklonjeni ste s košnice '{beehive?.Name ?? "Nepoznato"}'.",
                NotificationType.BeehiveUnassigned,
                beehiveId, nameof(Beehive));
        }

        var updated = await _uow.Users.GetByIdWithAssignedBeehivesAsync(memberId);
        return MapMember(updated!);
    }

    public async Task<OrgMemberDto> UpdateApiaryAssignmentAsync(int memberId, UpdateApiaryAssignmentDto dto)
    {
        var orgId = RequireOrganization();

        var member = await _uow.Users.GetByIdWithOrganizationAsync(memberId)
            ?? throw new NotFoundException(nameof(User), memberId);

        if (member.OrganizationId != orgId)
            throw new ForbiddenAccessException("This member does not belong to your organization.");

        if (member.Role != UserRole.ApiaryAdmin)
            throw new BusinessRuleException("Apiary assignment can only be set for Admin-role members.");

        Apiary? newApiary = null;
        if (dto.ApiaryId.HasValue)
        {
            newApiary = await _uow.Apiaries.GetByIdAsync(dto.ApiaryId.Value)
                ?? throw new NotFoundException(nameof(Apiary), dto.ApiaryId.Value);

            if (newApiary.OrganizationId != orgId)
                throw new BusinessRuleException("The selected apiary does not belong to your organization.");
        }

        var oldApiaryId = member.ApiaryId;
        member.ApiaryId = dto.ApiaryId;
        await _uow.Users.UpdateAsync(member);

        // apiaryId is a JWT claim — an existing session would keep pointing at the old apiary.
        if (dto.ApiaryId != oldApiaryId)
            await _sessions.RevokeAllAsync(memberId);

        await _uow.SaveChangesAsync();

        // Notify admin of apiary assignment change
        if (dto.ApiaryId.HasValue && dto.ApiaryId != oldApiaryId && newApiary != null)
        {
            await _notifications.NotifyAsync(
                memberId,
                "Pčelinjak dodijeljen",
                $"Dodijeljeni ste kao Admin pčelinjaka '{newApiary.Name}'.",
                NotificationType.ApiaryAssigned,
                dto.ApiaryId.Value, nameof(Apiary));
        }
        else if (!dto.ApiaryId.HasValue && oldApiaryId.HasValue)
        {
            await _notifications.NotifyAsync(
                memberId,
                "Pčelinjak uklonjen",
                "Uklonjeni ste s vašeg pčelinjaka.",
                NotificationType.ApiaryUnassigned);
        }

        var updated = await _uow.Users.GetByIdWithAssignedBeehivesAsync(memberId);
        return MapMember(updated!);
    }

    public async Task<IEnumerable<OrgAvailableBeehiveDto>> GetAvailableBeehivesAsync()
    {
        if (_currentUser.OrganizationId is not int orgId)
            return [];

        var beehives = await _uow.Beehives.GetByOrganizationAsync(orgId);

        // An ApiaryAdmin may only assign beehives from their own apiary.
        if (_currentUser.Role == UserRole.ApiaryAdmin && _currentUser.ApiaryId is int callerApiaryId)
            beehives = beehives.Where(b => b.ApiaryId == callerApiaryId);

        return beehives.Select(b => new OrgAvailableBeehiveDto
        {
            Id = b.Id,
            Name = b.Name,
            ApiaryName = b.Apiary?.Name ?? string.Empty,
        });
    }

    public async Task<IEnumerable<OrgAvailableApiaryDto>> GetAvailableApiariesAsync()
    {
        if (_currentUser.OrganizationId is not int orgId)
            return [];

        var apiaries = await _uow.Apiaries.GetAllByOrganizationAsync(orgId);
        return apiaries.Select(a => new OrgAvailableApiaryDto { Id = a.Id, Name = a.Name });
    }

    public async Task<OrgMemberDto> CreateMemberAsync(CreateOrgMemberDto dto)
    {
        var orgId = RequireOrganization();

        if (_currentUser.Role != UserRole.OrganizationAdmin)
            throw new ForbiddenAccessException("Only Organization Admins can add new members.");

        await _plan.EnsureCanAddMemberAsync(orgId);

        if (!Enum.TryParse<UserRole>(dto.Role, out var role) || role is not (UserRole.ApiaryAdmin or UserRole.Beekeeper))
            throw new BusinessRuleException("Role must be ApiaryAdmin or Beekeeper.");

        var existing = await _uow.Users.GetByEmailAsync(dto.Email.Trim().ToLower());
        if (existing != null)
            throw new BusinessRuleException($"A user with email '{dto.Email}' already exists.");

        // The validator has already rejected an unparseable number, so this cannot be null here.
        // The check is platform-wide, not org-scoped: the number is a global login identifier.
        var phone = PhoneRules.Normalize(dto.Phone)!;
        if (await _uow.Users.IsPhoneTakenAsync(phone))
            throw new BusinessRuleException(PhoneRules.DuplicateMessage);

        Apiary? apiary = null;
        if (role == UserRole.ApiaryAdmin)
        {
            if (!dto.ApiaryId.HasValue)
                throw new BusinessRuleException("Admin members must be assigned to a specific apiary.");

            apiary = await _uow.Apiaries.GetByIdAsync(dto.ApiaryId.Value)
                ?? throw new NotFoundException(nameof(Apiary), dto.ApiaryId.Value);

            if (apiary.OrganizationId != orgId)
                throw new BusinessRuleException("The selected apiary does not belong to your organization.");
        }

        if (role == UserRole.Beekeeper)
        {
            foreach (var beehiveId in dto.AssignedBeehiveIds)
            {
                var beehive = await _uow.Beehives.GetByIdAsync(beehiveId)
                    ?? throw new NotFoundException(nameof(Beehive), beehiveId);

                var beehiveApiary = await _uow.Apiaries.GetByIdAsync(beehive.ApiaryId)
                    ?? throw new NotFoundException(nameof(Apiary), beehive.ApiaryId);

                if (beehiveApiary.OrganizationId != orgId)
                    throw new BusinessRuleException($"Beehive '{beehive.Name}' does not belong to your organization.");
            }
        }

        var user = new User
        {
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Email = dto.Email.Trim().ToLower(),
            Phone = phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = role,
            OrganizationId = orgId,
            ApiaryId = role == UserRole.ApiaryAdmin ? dto.ApiaryId : null,
            CreatedAt = DateTime.UtcNow,
        };

        await _uow.Users.AddAsync(user);
        await _uow.SaveChangesAsync();

        if (role == UserRole.Beekeeper && dto.AssignedBeehiveIds.Count > 0)
        {
            await _uow.Users.SetBeehiveAssignmentsAsync(user.Id, dto.AssignedBeehiveIds);
            await _uow.SaveChangesAsync();
        }

        await _notifications.NotifyAsync(
            user.Id,
            "Dobrodošli u Melarium!",
            $"Vaš račun je kreiran. Možete se prijaviti s e-poštom: {user.Email}.",
            NotificationType.AccountCreated);

        if (role == UserRole.ApiaryAdmin && apiary != null)
        {
            await _notifications.NotifyAsync(
                user.Id,
                "Pčelinjak dodijeljen",
                $"Dodijeljeni ste kao Admin pčelinjaka '{apiary.Name}'.",
                NotificationType.ApiaryAssigned,
                dto.ApiaryId!.Value, nameof(Apiary));
        }

        if (role == UserRole.Beekeeper)
        {
            foreach (var beehiveId in dto.AssignedBeehiveIds)
            {
                var beehive = await _uow.Beehives.GetByIdAsync(beehiveId);
                if (beehive == null) continue;
                await _notifications.NotifyAsync(
                    user.Id,
                    "Košnica dodijeljena",
                    $"Dodijeljeni ste košnici '{beehive.Name}'.",
                    NotificationType.BeehiveAssigned,
                    beehiveId, nameof(Beehive));
            }
        }

        var created = await _uow.Users.GetByIdWithAssignedBeehivesAsync(user.Id);
        return MapMember(created!);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private int RequireOrganization() =>
        _currentUser.OrganizationId
            ?? throw new ForbiddenAccessException("You must belong to an organization to manage its members.");

    private static OrgMemberDto MapMember(User u) => new()
    {
        Id = u.Id,
        FirstName = u.FirstName,
        LastName = u.LastName,
        Email = u.Email,
        Phone = u.Phone,
        Role = u.Role.ToString(),
        ApiaryId = u.ApiaryId,
        ApiaryName = u.Apiary?.Name,
        AssignedBeehiveIds = u.AssignedBeehives.Select(ub => ub.BeehiveId).ToList(),
        AssignedBeehiveNames = u.AssignedBeehives
            .Select(ub => ub.Beehive?.Name ?? string.Empty)
            .Where(n => n != string.Empty)
            .ToList(),
    };
}
