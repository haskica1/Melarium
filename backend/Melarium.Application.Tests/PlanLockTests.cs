using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Downgrade locking against the database side of the rules (SPEC-24): which accounts lose write
/// access when an organization has more members than its plan seats, and that the locked sets are
/// computed from the <i>effective</i> plan, so an expired plan behaves as Free here too.
/// </summary>
public class PlanLockTests
{
    private static readonly DateTime Day = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private static readonly Dictionary<string, string?> DefaultPlans = new()
    {
        ["Plans:Free:MaxApiaries"] = "1",
        ["Plans:Free:MaxBeehives"] = "7",
        ["Plans:Free:MaxMembers"] = "0",
        ["Plans:Standard:MaxBeehives"] = "30",
        ["Plans:Standard:MaxMembers"] = "2",
        ["Plans:Pro:MaxBeehives"] = "100",
        ["Plans:Pro:MaxMembers"] = "5",
    };

    private static IConfiguration Config()
    {
        var config = Substitute.For<IConfiguration>();
        config[Arg.Any<string>()].Returns(ci => DefaultPlans.GetValueOrDefault(ci.Arg<string>()));
        return config;
    }

    private PlanLock Lock(UserRole role = UserRole.Beekeeper, int userId = 2, int? orgId = 1) =>
        new(_uow, new TestCurrentUser { UserId = userId, Role = role, OrganizationId = orgId }, Config());

    private void OrgOnPlan(PlanType plan, DateTime? validUntil = null) =>
        _uow.Organizations.GetByIdAsync(1)
            .Returns(new Organization { Id = 1, Name = "Org", Plan = plan, PlanValidUntil = validUntil });

    /// <summary>Owner first (an OrganizationAdmin), then members in the given order, oldest first.</summary>
    private void MembersAre(params (int Id, UserRole Role, int DayOffset)[] users) =>
        _uow.Users.GetByOrganizationWithDetailsAsync(1).Returns(
            users.Select(u => new User { Id = u.Id, Role = u.Role, OrganizationId = 1, CreatedAt = Day.AddDays(u.DayOffset) }).ToList());

    private void OrgHas(int apiaries, int beehives)
    {
        var apiaryRows = Enumerable.Range(1, apiaries)
            .Select(i => new Apiary { Id = i, Name = $"P{i}", OrganizationId = 1, CreatedAt = Day.AddDays(i) })
            .ToList();

        var hiveRows = Enumerable.Range(1, beehives)
            .Select(i => new Beehive { Id = 100 + i, Name = $"K{i}", ApiaryId = 1, CreatedAt = Day.AddDays(i) })
            .ToList();

        _uow.Apiaries.GetAllByOrganizationAsync(1).Returns(apiaryRows);
        _uow.Beehives.GetByOrganizationAsync(1).Returns(hiveRows);
    }

    // ── Read-only members ────────────────────────────────────────────────────────

    [Fact]
    public async Task Owner_IsNeverReadOnly_EvenOnFree()
    {
        OrgOnPlan(PlanType.Free);
        MembersAre((1, UserRole.OrganizationAdmin, 0), (2, UserRole.Beekeeper, 1));

        Assert.False(await Lock(UserRole.OrganizationAdmin, userId: 1).IsCurrentUserReadOnlyAsync());
    }

    [Fact]
    public async Task Free_MakesEveryAdditionalMemberReadOnly()
    {
        OrgOnPlan(PlanType.Free);
        MembersAre((1, UserRole.OrganizationAdmin, 0), (2, UserRole.Beekeeper, 1), (3, UserRole.Beekeeper, 2));

        Assert.True(await Lock(userId: 2).IsCurrentUserReadOnlyAsync());
        Assert.True(await Lock(userId: 3).IsCurrentUserReadOnlyAsync());
    }

    [Fact]
    public async Task Standard_KeepsTheTwoOldestMembersWriting_AndFreezesTheRest()
    {
        OrgOnPlan(PlanType.Standard);
        MembersAre(
            (1, UserRole.OrganizationAdmin, 0),
            (2, UserRole.Beekeeper, 1),
            (3, UserRole.Beekeeper, 2),
            (4, UserRole.Beekeeper, 3));

        Assert.False(await Lock(userId: 2).IsCurrentUserReadOnlyAsync());
        Assert.False(await Lock(userId: 3).IsCurrentUserReadOnlyAsync());
        Assert.True(await Lock(userId: 4).IsCurrentUserReadOnlyAsync());
    }

    [Fact]
    public async Task UnlimitedMembers_FreezesNobody()
    {
        OrgOnPlan(PlanType.Max);   // no MaxMembers key = unlimited
        MembersAre((1, UserRole.OrganizationAdmin, 0), (2, UserRole.Beekeeper, 1), (3, UserRole.Beekeeper, 2));

        Assert.False(await Lock(userId: 3).IsCurrentUserReadOnlyAsync());
    }

    [Fact]
    public async Task SystemAdmin_IsNeverReadOnly()
    {
        OrgOnPlan(PlanType.Free);
        MembersAre((1, UserRole.OrganizationAdmin, 0), (2, UserRole.SystemAdmin, 1));

        Assert.False(await Lock(UserRole.SystemAdmin, userId: 2).IsCurrentUserReadOnlyAsync());
    }

    // ── Locked sets follow the effective plan ────────────────────────────────────

    [Fact]
    public async Task ExpiredProTrial_LocksLikeFree()
    {
        // The trial that started this: Pro, expired yesterday, 2 apiaries and 10 hives created under it.
        OrgOnPlan(PlanType.Pro, validUntil: DateTime.UtcNow.Date.AddDays(-1));
        OrgHas(apiaries: 2, beehives: 10);

        var locked = await Lock().GetForOrganizationAsync(1);

        Assert.Equal([2], locked.ApiaryIds);            // only the oldest apiary survives
        Assert.Equal(3, locked.BeehiveIds.Count);       // 10 hives, 7 slots
    }

    [Fact]
    public async Task ProStillValid_LocksNothing()
    {
        OrgOnPlan(PlanType.Pro, validUntil: DateTime.UtcNow.Date.AddDays(5));
        OrgHas(apiaries: 2, beehives: 10);

        Assert.True((await Lock().GetForOrganizationAsync(1)).IsEmpty);
    }

    [Fact]
    public async Task SystemAdmin_SeesNothingLocked()
    {
        OrgOnPlan(PlanType.Free);
        OrgHas(apiaries: 3, beehives: 40);

        Assert.True((await Lock(UserRole.SystemAdmin, orgId: null).GetForOrganizationAsync(1)).IsEmpty);
    }

    [Fact]
    public async Task PreviewForPlan_IgnoresTheCurrentPlan_SoTheWarningCanLookAhead()
    {
        // Still on a valid Pro plan — nothing is locked today, but Free would lock plenty.
        OrgOnPlan(PlanType.Pro, validUntil: DateTime.UtcNow.Date.AddDays(2));
        OrgHas(apiaries: 3, beehives: 40);

        var today = await Lock().GetForOrganizationAsync(1);
        var pending = await Lock().PreviewForPlanAsync(1, PlanType.Free);

        Assert.True(today.IsEmpty);
        Assert.Equal(2, pending.ApiaryIds.Count);
        Assert.Equal(33, pending.BeehiveIds.Count);
    }

    // ── Refusals ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EnsureBeehiveUnlocked_ThrowsPlanLimit_ForALockedHive()
    {
        OrgOnPlan(PlanType.Free);
        OrgHas(apiaries: 1, beehives: 10);
        _uow.Beehives.GetByIdAsync(110).Returns(new Beehive { Id = 110, ApiaryId = 1 });
        _uow.Apiaries.GetByIdAsync(1).Returns(new Apiary { Id = 1, OrganizationId = 1 });

        await Assert.ThrowsAsync<Common.Exceptions.PlanLimitException>(
            () => Lock().EnsureBeehiveUnlockedAsync(110));
    }

    [Fact]
    public async Task EnsureBeehiveUnlocked_Passes_ForAHiveWithinTheLimit()
    {
        OrgOnPlan(PlanType.Free);
        OrgHas(apiaries: 1, beehives: 10);
        _uow.Beehives.GetByIdAsync(101).Returns(new Beehive { Id = 101, ApiaryId = 1 });
        _uow.Apiaries.GetByIdAsync(1).Returns(new Apiary { Id = 1, OrganizationId = 1 });

        await Lock().EnsureBeehiveUnlockedAsync(101);   // does not throw
    }
}
