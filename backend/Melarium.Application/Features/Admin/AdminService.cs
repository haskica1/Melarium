using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Common.Validation;
using Melarium.Application.Features.Admin.DTOs;
using Melarium.Application.Features.Notifications;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Admin;

public class AdminService : IAdminService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifications;
    private readonly ISessionRevoker _sessions;

    public AdminService(IUnitOfWork uow, INotificationService notifications, ISessionRevoker sessions)
    {
        _uow           = uow;
        _notifications = notifications;
        _sessions      = sessions;
    }

    // ── Organizations ──────────────────────────────────────────────────────────

    public async Task<IEnumerable<AdminOrganizationDto>> GetAllOrganizationsAsync()
    {
        var orgs = await _uow.Organizations.GetAllWithDetailsAsync();
        var beehiveCounts = await _uow.Organizations.GetBeehiveCountsAsync();
        var lastActivity  = await _uow.Organizations.GetLastActivityAsync();

        return orgs
            .Select(o => MapOrganization(o, beehiveCounts, lastActivity))
            .ToList();
    }

    public async Task<AdminOrganizationDto> GetOrganizationByIdAsync(int id)
    {
        var org = await _uow.Organizations.GetWithDetailsAsync(id)
            ?? throw new NotFoundException(nameof(Organization), id);

        var beehiveCounts = await _uow.Organizations.GetBeehiveCountsAsync(id);
        var lastActivity  = await _uow.Organizations.GetLastActivityAsync(id);

        return MapOrganization(org, beehiveCounts, lastActivity);
    }

    public async Task<AdminOrganizationDto> CreateOrganizationAsync(CreateOrganizationDto dto, int? createdById)
    {
        var org = new Organization
        {
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Organizations.AddAsync(org);
        await _uow.SaveChangesAsync();

        return await GetOrganizationByIdAsync(org.Id);
    }

    public async Task<AdminOrganizationDto> UpdateOrganizationAsync(int id, UpdateOrganizationDto dto)
    {
        var org = await _uow.Organizations.GetWithDetailsAsync(id)
            ?? throw new NotFoundException(nameof(Organization), id);

        org.Name = dto.Name.Trim();
        org.Description = dto.Description?.Trim();

        await _uow.Organizations.UpdateAsync(org);
        await _uow.SaveChangesAsync();

        return await GetOrganizationByIdAsync(id);
    }

    public async Task<AdminOrganizationDto> UpdateOrganizationPlanAsync(int id, UpdateOrganizationPlanDto dto)
    {
        var org = await _uow.Organizations.GetWithDetailsAsync(id)
            ?? throw new NotFoundException(nameof(Organization), id);

        // Manual activation (SPEC-09 v1): SystemAdmin sets the plan after a bank-transfer payment;
        // null PlanValidUntil = bez isteka (doživotno — early adopters / Partner orgs).
        org.Plan = dto.Plan;
        org.PlanValidUntil = dto.PlanValidUntil;
        org.PlanNotes = string.IsNullOrWhiteSpace(dto.PlanNotes) ? null : dto.PlanNotes.Trim();

        await _uow.Organizations.UpdateAsync(org);
        await _uow.SaveChangesAsync();

        return await GetOrganizationByIdAsync(id);
    }

    public async Task DeleteOrganizationAsync(int id)
    {
        var org = await _uow.Organizations.GetWithDetailsAsync(id)
            ?? throw new NotFoundException(nameof(Organization), id);

        if (org.Users.Count > 0)
            throw new BusinessRuleException(
                $"Cannot delete organization '{org.Name}' because it has {org.Users.Count} user(s). Remove or reassign users first.");

        await _uow.Organizations.DeleteAsync(org);
        await _uow.SaveChangesAsync();
    }

    // ── Apiaries ───────────────────────────────────────────────────────────────

    public async Task<IEnumerable<AdminApiaryListItemDto>> GetApiariesByOrganizationAsync(int organizationId)
    {
        var apiaries = await _uow.Apiaries.GetAllByOrganizationAsync(organizationId);
        return apiaries.Select(a => new AdminApiaryListItemDto { Id = a.Id, Name = a.Name });
    }

    public async Task<IEnumerable<AdminBeehiveListItemDto>> GetBeehivesByOrganizationAsync(int organizationId)
    {
        var beehives = await _uow.Beehives.GetByOrganizationAsync(organizationId);
        return beehives.Select(b => new AdminBeehiveListItemDto
        {
            Id = b.Id,
            Name = b.Name,
            ApiaryName = b.Apiary?.Name ?? string.Empty,
        });
    }

    // ── Users ──────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<AdminUserDto>> GetAllUsersAsync()
    {
        // AssignedBeehives is included by the repository — one query for all users
        // instead of an extra round-trip per Beekeeper.
        var users = await _uow.Users.GetAllWithOrganizationAsync();
        return users.Select(MapUser).ToList();
    }

    public async Task<AdminUserDto> GetUserByIdAsync(int id)
    {
        var user = await _uow.Users.GetByIdWithAssignedBeehivesAsync(id)
            ?? throw new NotFoundException(nameof(User), id);
        return MapUser(user);
    }

    public async Task<AdminUserDto> CreateUserAsync(CreateAdminUserDto dto)
    {
        var existing = await _uow.Users.GetByEmailAsync(dto.Email.Trim().ToLower());
        if (existing != null)
            throw new BusinessRuleException($"A user with email '{dto.Email}' already exists.");

        // The validator has already rejected an unparseable number, so this cannot be null here.
        var phone = PhoneRules.Normalize(dto.Phone)!;
        if (await _uow.Users.IsPhoneTakenAsync(phone))
            throw new BusinessRuleException(PhoneRules.DuplicateMessage);

        if (!Enum.TryParse<UserRole>(dto.Role, out var role))
            throw new BusinessRuleException($"Invalid role '{dto.Role}'.");

        ValidateRoleOrgApiaryConsistency(role, dto.OrganizationId, dto.ApiaryId);

        if (dto.OrganizationId.HasValue)
        {
            var orgExists = await _uow.Organizations.ExistsAsync(dto.OrganizationId.Value);
            if (!orgExists)
                throw new NotFoundException(nameof(Organization), dto.OrganizationId.Value);
        }

        Apiary? apiary = null;
        if (dto.ApiaryId.HasValue)
        {
            apiary = await _uow.Apiaries.GetByIdAsync(dto.ApiaryId.Value);
            if (apiary == null)
                throw new NotFoundException(nameof(Apiary), dto.ApiaryId.Value);
            if (dto.OrganizationId.HasValue && apiary.OrganizationId != dto.OrganizationId.Value)
                throw new BusinessRuleException("The selected apiary does not belong to the selected organization.");
        }

        var user = new User
        {
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Email = dto.Email.Trim().ToLower(),
            Phone = phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = role,
            OrganizationId = dto.OrganizationId,
            ApiaryId = dto.ApiaryId,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Users.AddAsync(user);
        await _uow.SaveChangesAsync();

        if (role == UserRole.Beekeeper && dto.AssignedBeehiveIds.Count > 0)
        {
            await _uow.Users.SetBeehiveAssignmentsAsync(user.Id, dto.AssignedBeehiveIds);
            await _uow.SaveChangesAsync();
        }

        var created = await _uow.Users.GetByIdWithAssignedBeehivesAsync(user.Id);

        // ── Notifications ──────────────────────────────────────────────────────
        var fullName = $"{user.FirstName} {user.LastName}";

        // 4) Account created — always notify the new user
        await _notifications.NotifyAsync(
            user.Id,
            "Dobrodošli u Melarium!",
            $"Vaš račun je kreiran. Možete se prijaviti s e-poštom: {user.Email}.",
            NotificationType.AccountCreated);

        // 1) OrgAdmin assigned to an organisation
        if (role == UserRole.OrganizationAdmin && dto.OrganizationId.HasValue)
        {
            var org = await _uow.Organizations.GetWithDetailsAsync(dto.OrganizationId.Value);
            await _notifications.NotifyAsync(
                user.Id,
                "Organizacija dodijeljena",
                $"Dodijeljeni ste kao administrator organizacije '{org?.Name}'.",
                NotificationType.OrganizationAssigned,
                dto.OrganizationId.Value, nameof(Organization));
        }

        // 2) Admin assigned to an apiary
        if (role == UserRole.ApiaryAdmin && dto.ApiaryId.HasValue && apiary != null)
        {
            await _notifications.NotifyAsync(
                user.Id,
                "Pčelinjak dodijeljen",
                $"Dodijeljeni ste kao Admin pčelinjaka '{apiary.Name}'.",
                NotificationType.ApiaryAssigned,
                dto.ApiaryId.Value, nameof(Apiary));
        }

        // 3) User assigned to beehives
        if (role == UserRole.Beekeeper && dto.AssignedBeehiveIds.Count > 0)
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

        return MapUser(created!);
    }

    public async Task<AdminUserDto> UpdateUserAsync(int id, UpdateAdminUserDto dto)
    {
        var user = await _uow.Users.GetByIdWithAssignedBeehivesAsync(id)
            ?? throw new NotFoundException(nameof(User), id);

        if (!Enum.TryParse<UserRole>(dto.Role, out var role))
            throw new BusinessRuleException($"Invalid role '{dto.Role}'.");

        ValidateRoleOrgApiaryConsistency(role, dto.OrganizationId, dto.ApiaryId);

        if (dto.OrganizationId.HasValue)
        {
            var orgExists = await _uow.Organizations.ExistsAsync(dto.OrganizationId.Value);
            if (!orgExists)
                throw new NotFoundException(nameof(Organization), dto.OrganizationId.Value);
        }

        Apiary? newApiary = null;
        if (dto.ApiaryId.HasValue)
        {
            newApiary = await _uow.Apiaries.GetByIdAsync(dto.ApiaryId.Value);
            if (newApiary == null)
                throw new NotFoundException(nameof(Apiary), dto.ApiaryId.Value);
            if (dto.OrganizationId.HasValue && newApiary.OrganizationId != dto.OrganizationId.Value)
                throw new BusinessRuleException("The selected apiary does not belong to the selected organization.");
        }

        var newEmail = dto.Email.Trim().ToLower();
        if (!string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            var conflict = await _uow.Users.GetByEmailAsync(newEmail);
            if (conflict != null)
                throw new BusinessRuleException($"A user with email '{dto.Email}' already exists.");
        }

        // Blank means "leave the stored number alone", never "clear it" — an older client that
        // doesn't send the field must not silently strip a login identifier off the account.
        var newPhone = PhoneRules.Normalize(dto.Phone);
        if (newPhone is not null && newPhone != user.Phone)
        {
            if (await _uow.Users.IsPhoneTakenAsync(newPhone, user.Id))
                throw new BusinessRuleException(PhoneRules.DuplicateMessage);

            user.Phone = newPhone;
        }

        // Capture old state for change detection
        var oldRole      = user.Role;
        var oldOrgId     = user.OrganizationId;
        var oldApiaryId  = user.ApiaryId;
        var oldBeehiveIds = user.AssignedBeehives.Select(ub => ub.BeehiveId).ToHashSet();

        user.FirstName = dto.FirstName.Trim();
        user.LastName = dto.LastName.Trim();
        user.Email = newEmail;
        user.Role = role;
        user.OrganizationId = dto.OrganizationId;
        user.ApiaryId = dto.ApiaryId;

        await _uow.Users.UpdateAsync(user);

        var newBeehiveIds = role == UserRole.Beekeeper ? dto.AssignedBeehiveIds : new List<int>();
        await _uow.Users.SetBeehiveAssignmentsAsync(id, newBeehiveIds);

        // Role, organisation and apiary are baked into the JWT. Leaving old sessions alive would
        // let a demoted user keep acting with their previous privileges until the token expires.
        // (Beehive assignments are resolved per request from the database, so they need no revoke.)
        if (role != oldRole || dto.OrganizationId != oldOrgId || dto.ApiaryId != oldApiaryId)
            await _sessions.RevokeAllAsync(id);

        await _uow.SaveChangesAsync();

        var updated = await _uow.Users.GetByIdWithAssignedBeehivesAsync(id);

        // ── Notifications for detected changes ────────────────────────────────

        // 1) Org assignment for OrgAdmin
        if (role == UserRole.OrganizationAdmin)
        {
            if (dto.OrganizationId.HasValue && dto.OrganizationId != oldOrgId)
            {
                var org = await _uow.Organizations.GetWithDetailsAsync(dto.OrganizationId.Value);
                await _notifications.NotifyAsync(
                    user.Id,
                    "Organizacija dodijeljena",
                    $"Dodijeljeni ste kao administrator organizacije '{org?.Name}'.",
                    NotificationType.OrganizationAssigned,
                    dto.OrganizationId.Value, nameof(Organization));
            }
            else if (!dto.OrganizationId.HasValue && oldOrgId.HasValue)
            {
                await _notifications.NotifyAsync(
                    user.Id,
                    "Organizacija uklonjena",
                    "Uklonjeni ste iz vaše organizacije.",
                    NotificationType.OrganizationUnassigned);
            }
        }
        else if (oldRole == UserRole.OrganizationAdmin && oldOrgId.HasValue)
        {
            // Role changed away from OrgAdmin
            await _notifications.NotifyAsync(
                user.Id,
                "Organizacija uklonjena",
                "Uklonjeni ste iz vaše organizacije.",
                NotificationType.OrganizationUnassigned);
        }

        // 2) Apiary assignment for Admin
        if (role == UserRole.ApiaryAdmin)
        {
            if (dto.ApiaryId.HasValue && dto.ApiaryId != oldApiaryId && newApiary != null)
            {
                await _notifications.NotifyAsync(
                    user.Id,
                    "Pčelinjak dodijeljen",
                    $"Dodijeljeni ste kao Admin pčelinjaka '{newApiary.Name}'.",
                    NotificationType.ApiaryAssigned,
                    dto.ApiaryId.Value, nameof(Apiary));
            }
            else if (!dto.ApiaryId.HasValue && oldApiaryId.HasValue)
            {
                await _notifications.NotifyAsync(
                    user.Id,
                    "Pčelinjak uklonjen",
                    "Uklonjeni ste s vašeg pčelinjaka.",
                    NotificationType.ApiaryUnassigned);
            }
        }
        else if (oldRole == UserRole.ApiaryAdmin && oldApiaryId.HasValue)
        {
            await _notifications.NotifyAsync(
                user.Id,
                "Pčelinjak uklonjen",
                "Uklonjeni ste s vašeg pčelinjaka.",
                NotificationType.ApiaryUnassigned);
        }

        // 3) Beehive assignment for User
        if (role == UserRole.Beekeeper)
        {
            var newSet = newBeehiveIds.ToHashSet();
            var added   = newSet.Except(oldBeehiveIds).ToList();
            var removed = oldBeehiveIds.Except(newSet).ToList();

            foreach (var beehiveId in added)
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

            foreach (var beehiveId in removed)
            {
                var beehive = await _uow.Beehives.GetByIdAsync(beehiveId);
                await _notifications.NotifyAsync(
                    user.Id,
                    "Košnica uklonjena",
                    $"Uklonjeni ste s košnice '{beehive?.Name ?? "Nepoznato"}'.",
                    NotificationType.BeehiveUnassigned,
                    beehiveId, nameof(Beehive));
            }
        }
        else if (oldRole == UserRole.Beekeeper && oldBeehiveIds.Count > 0)
        {
            // Role changed away from User — all beehive assignments removed
            foreach (var beehiveId in oldBeehiveIds)
            {
                var beehive = await _uow.Beehives.GetByIdAsync(beehiveId);
                await _notifications.NotifyAsync(
                    user.Id,
                    "Košnica uklonjena",
                    $"Uklonjeni ste s košnice '{beehive?.Name ?? "Nepoznato"}'.",
                    NotificationType.BeehiveUnassigned,
                    beehiveId, nameof(Beehive));
            }
        }

        return MapUser(updated!);
    }

    public async Task DeleteUserAsync(int id)
    {
        var user = await _uow.Users.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(User), id);

        if (user.Role == UserRole.SystemAdmin)
        {
            // COUNT in SQL rather than materialising every user just to count one role.
            var adminCount = await _uow.Users.CountByRoleAsync(UserRole.SystemAdmin);
            if (adminCount <= 1)
                throw new BusinessRuleException("Cannot delete the last SystemAdmin account.");
        }

        await _uow.Users.DeleteAsync(user);
        await _uow.SaveChangesAsync();
    }

    // ── Validation ─────────────────────────────────────────────────────────────

    private static void ValidateRoleOrgApiaryConsistency(UserRole role, int? organizationId, int? apiaryId)
    {
        if (role == UserRole.SystemAdmin)
        {
            if (organizationId != null)
                throw new BusinessRuleException("SystemAdmin users cannot belong to an organization.");
            return;
        }

        if (organizationId == null)
            throw new BusinessRuleException("Non-SystemAdmin users must belong to an organization.");

        if (role == UserRole.ApiaryAdmin && apiaryId == null)
            throw new BusinessRuleException("Admin users must be assigned to a specific apiary.");

        if (role != UserRole.ApiaryAdmin && apiaryId != null)
            throw new BusinessRuleException("Only Admin users can be assigned to a specific apiary.");
    }

    // ── Mappers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Hive count and last activity are absent from the dictionaries for an organization that has
    /// neither, which is why they are looked up rather than read off the entity: 0 hives and "never
    /// active" are real answers, not missing data.
    /// </summary>
    private static AdminOrganizationDto MapOrganization(
        Organization o,
        Dictionary<int, int> beehiveCounts,
        Dictionary<int, DateTime> lastActivity) => new()
    {
        Id = o.Id,
        Name = o.Name,
        Description = o.Description,
        UserCount = o.Users.Count,
        ApiaryCount = o.Apiaries.Count,
        BeehiveCount = beehiveCounts.GetValueOrDefault(o.Id),
        LastActivityAt = lastActivity.TryGetValue(o.Id, out var at) ? at : null,
        CreatedByName = o.CreatedBy != null ? $"{o.CreatedBy.FirstName} {o.CreatedBy.LastName}" : null,
        CreatedAt = o.CreatedAt,
        Plan = o.Plan,
        PlanName = Common.Localization.BsLabels.Label(o.Plan),
        PlanValidUntil = o.PlanValidUntil,
        PlanNotes = o.PlanNotes,
    };

    private static AdminUserDto MapUser(User u) => new()
    {
        Id = u.Id,
        FirstName = u.FirstName,
        LastName = u.LastName,
        Email = u.Email,
        Phone = u.Phone,
        Role = u.Role.ToString(),
        OrganizationId = u.OrganizationId,
        OrganizationName = u.Organization?.Name,
        ApiaryId = u.ApiaryId,
        ApiaryName = u.Apiary?.Name,
        AssignedBeehiveIds = u.AssignedBeehives.Select(ub => ub.BeehiveId).ToList(),
        CreatedAt = u.CreatedAt
    };
}
