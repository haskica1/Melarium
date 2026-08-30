using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Common.Interfaces;

/// <summary>User-specific data access operations.</summary>
public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);

    /// <summary>
    /// Looks a user up by phone number. The argument must already be canonical E.164
    /// (<c>PhoneRules.Normalize</c>) — stored numbers are canonical, so a raw string never matches.
    /// </summary>
    Task<User?> GetByPhoneAsync(string phone);

    /// <summary>
    /// True when the (already canonical) number belongs to some account other than
    /// <paramref name="excludeUserId"/>. The exclusion is what lets a user re-save their own
    /// profile without colliding with themselves.
    /// </summary>
    Task<bool> IsPhoneTakenAsync(string phone, int? excludeUserId = null);

    /// <summary>
    /// Resolves a "pozovi prijatelja" share link back to its owner (SPEC-15). Read-only lookup —
    /// tracked navigation properties are not needed and the caller must not mutate the result.
    /// </summary>
    Task<User?> GetByReferralCodeAsync(string referralCode);

    /// <summary>Every user on the platform. SystemAdmin screens only — see the org-scoped overload below.</summary>
    Task<IEnumerable<User>> GetAllWithOrganizationAsync();

    /// <summary>
    /// One organisation's users, with the same includes as <see cref="GetAllWithOrganizationAsync"/>.
    /// Org-scoped screens must use this: filtering the platform-wide list in memory grows with the
    /// number of tenants rather than the caller's own data.
    /// </summary>
    Task<IEnumerable<User>> GetByOrganizationWithDetailsAsync(int organizationId);

    /// <summary>Number of users holding the given role, platform-wide.</summary>
    Task<int> CountByRoleAsync(UserRole role);
    Task<User?> GetByIdWithOrganizationAsync(int id);
    Task<User?> GetByIdWithAssignedBeehivesAsync(int id);
    Task<bool> IsUserAssignedToBeehiveAsync(int userId, int beehiveId);
    Task SetBeehiveAssignmentsAsync(int userId, IEnumerable<int> beehiveIds);

    /// <summary>IDs of the beehives assigned to the user — no entities loaded.</summary>
    Task<HashSet<int>> GetAssignedBeehiveIdsAsync(int userId);

    /// <summary>IDs of the apiaries containing the user's assigned beehives — no entities loaded.</summary>
    Task<HashSet<int>> GetAssignedApiaryIdsAsync(int userId);

    // ── Recipient resolution for alerts (SPEC-04) ──

    /// <summary>User ids of every Beekeeper assigned to the given beehive.</summary>
    Task<List<int>> GetUserIdsAssignedToBeehiveAsync(int beehiveId);

    /// <summary>User ids of every Beekeeper assigned to at least one beehive in the given apiary.</summary>
    Task<List<int>> GetUserIdsAssignedToApiaryAsync(int apiaryId);

    /// <summary>User ids of the OrganizationAdmins of the given organization.</summary>
    Task<List<int>> GetOrganizationAdminIdsAsync(int organizationId);

    /// <summary>User ids of the ApiaryAdmins assigned to the given apiary.</summary>
    Task<List<int>> GetApiaryAdminIdsAsync(int apiaryId);

    /// <summary>Ids of every user on the platform — no entities loaded (SPEC-06 publish broadcast).</summary>
    Task<List<int>> GetAllIdsAsync();

    /// <summary>User ids of every SystemAdmin — in-app recipients for new feedback (SPEC-13).</summary>
    Task<List<int>> GetSystemAdminIdsAsync();

    /// <summary>Number of user accounts in the organization — plan member-limit checks (SPEC-09).</summary>
    Task<int> CountByOrganizationAsync(int organizationId);

    /// <summary>
    /// Newest issued session per user — <c>MAX(RefreshToken.CreatedAt)</c>, since a token row is written
    /// on sign-in and on every refresh. Read as "last time this account was actually used", which is
    /// what the SystemAdmin table labels "Zadnja prijava" (same source as ADR-034 org activity).
    /// Users who have never signed in are absent from the dictionary rather than present with a date.
    /// </summary>
    Task<Dictionary<int, DateTime>> GetLastLoginAtAsync(int? userId = null);
}
