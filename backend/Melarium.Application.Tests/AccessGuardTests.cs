using System.Linq.Expressions;
using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Locks the multi-tenant authorization matrix: SystemAdmin unrestricted, OrganizationAdmin
/// scoped to their org, ApiaryAdmin to their apiary, Beekeeper to explicitly assigned hives.
/// These rules fixed real cross-tenant bugs — regressions here are security bugs.
/// </summary>
public class AccessGuardTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IPlanLock _planLock = TestPlanLock.Unlocked();

    private AccessGuard CreateGuard(UserRole? role, int? userId = 1, int? organizationId = null, int? apiaryId = null) =>
        new(new TestCurrentUser { UserId = userId, Role = role, OrganizationId = organizationId, ApiaryId = apiaryId }, _uow, _planLock);

    // ── EnsureInOrganization ───────────────────────────────────────────────────

    [Fact]
    public void EnsureInOrganization_SystemAdmin_AnyOrganization_Passes()
    {
        var guard = CreateGuard(UserRole.SystemAdmin);
        guard.EnsureInOrganization(42); // does not throw
    }

    [Fact]
    public void EnsureInOrganization_MemberOfSameOrganization_Passes()
    {
        var guard = CreateGuard(UserRole.OrganizationAdmin, organizationId: 7);
        guard.EnsureInOrganization(7);
    }

    [Fact]
    public void EnsureInOrganization_MemberOfOtherOrganization_Throws()
    {
        var guard = CreateGuard(UserRole.OrganizationAdmin, organizationId: 7);
        Assert.Throws<ForbiddenAccessException>(() => guard.EnsureInOrganization(8));
    }

    // ── EnsureCanManageApiary (sync) ───────────────────────────────────────────

    [Theory]
    [InlineData(UserRole.SystemAdmin, null, null, true)]
    [InlineData(UserRole.OrganizationAdmin, 7, null, true)]   // same org
    [InlineData(UserRole.ApiaryAdmin, 7, 3, true)]            // same apiary
    [InlineData(UserRole.Beekeeper, 7, null, false)]          // never manages apiaries
    public void EnsureCanManageApiary_RoleMatrix(UserRole role, int? orgId, int? apiaryId, bool allowed)
    {
        var guard = CreateGuard(role, organizationId: orgId, apiaryId: apiaryId);

        if (allowed)
            guard.EnsureCanManageApiary(apiaryId: 3, organizationId: 7);
        else
            Assert.Throws<ForbiddenAccessException>(() => guard.EnsureCanManageApiary(3, 7));
    }

    [Fact]
    public void EnsureCanManageApiary_OrganizationAdminOfOtherOrg_Throws()
    {
        var guard = CreateGuard(UserRole.OrganizationAdmin, organizationId: 7);
        Assert.Throws<ForbiddenAccessException>(() => guard.EnsureCanManageApiary(3, organizationId: 99));
    }

    [Fact]
    public void EnsureCanManageApiary_ApiaryAdminOfOtherApiary_Throws()
    {
        var guard = CreateGuard(UserRole.ApiaryAdmin, organizationId: 7, apiaryId: 3);
        Assert.Throws<ForbiddenAccessException>(() => guard.EnsureCanManageApiary(4, organizationId: 7));
    }

    // ── EnsureCanManageApiaryAsync ─────────────────────────────────────────────

    [Fact]
    public async Task EnsureCanManageApiaryAsync_OrganizationAdmin_ResolvesApiaryOrg()
    {
        _uow.Apiaries.GetByIdAsync(3).Returns(new Apiary { Id = 3, OrganizationId = 7 });
        var guard = CreateGuard(UserRole.OrganizationAdmin, organizationId: 7);

        await guard.EnsureCanManageApiaryAsync(3); // does not throw
    }

    [Fact]
    public async Task EnsureCanManageApiaryAsync_OrganizationAdmin_ForeignApiary_Throws()
    {
        _uow.Apiaries.GetByIdAsync(3).Returns(new Apiary { Id = 3, OrganizationId = 99 });
        var guard = CreateGuard(UserRole.OrganizationAdmin, organizationId: 7);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => guard.EnsureCanManageApiaryAsync(3));
    }

    [Fact]
    public async Task EnsureCanManageApiaryAsync_MissingApiary_Throws()
    {
        _uow.Apiaries.GetByIdAsync(3).Returns((Apiary?)null);
        var guard = CreateGuard(UserRole.OrganizationAdmin, organizationId: 7);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => guard.EnsureCanManageApiaryAsync(3));
    }

    [Fact]
    public async Task EnsureCanManageApiaryAsync_ApiaryAdmin_OwnApiary_NoLookupNeeded()
    {
        var guard = CreateGuard(UserRole.ApiaryAdmin, organizationId: 7, apiaryId: 3);

        await guard.EnsureCanManageApiaryAsync(3);

        await _uow.Apiaries.DidNotReceive().GetByIdAsync(Arg.Any<int>());
    }

    // ── CanAccessBeehiveAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task CanAccessBeehive_SystemAdmin_True()
    {
        var guard = CreateGuard(UserRole.SystemAdmin);
        Assert.True(await guard.CanAccessBeehiveAsync(10));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CanAccessBeehive_Beekeeper_FollowsAssignment(bool assigned)
    {
        _uow.Users.IsUserAssignedToBeehiveAsync(1, 10).Returns(assigned);
        var guard = CreateGuard(UserRole.Beekeeper, organizationId: 7);

        Assert.Equal(assigned, await guard.CanAccessBeehiveAsync(10));
    }

    [Theory]
    [InlineData(3, true)]   // hive in the admin's apiary
    [InlineData(4, false)]  // hive in another apiary
    public async Task CanAccessBeehive_ApiaryAdmin_ScopedToApiary(int hiveApiaryId, bool expected)
    {
        _uow.Beehives.GetByIdAsync(10).Returns(new Beehive { Id = 10, ApiaryId = hiveApiaryId });
        var guard = CreateGuard(UserRole.ApiaryAdmin, organizationId: 7, apiaryId: 3);

        Assert.Equal(expected, await guard.CanAccessBeehiveAsync(10));
    }

    [Theory]
    [InlineData(7, true)]   // hive's apiary belongs to the admin's org
    [InlineData(99, false)] // hive's apiary belongs to another org
    public async Task CanAccessBeehive_OrganizationAdmin_ScopedToOrganization(int hiveOrgId, bool expected)
    {
        _uow.Beehives.GetByIdAsync(10).Returns(new Beehive { Id = 10, ApiaryId = 3 });
        _uow.Apiaries.GetByIdAsync(3).Returns(new Apiary { Id = 3, OrganizationId = hiveOrgId });
        var guard = CreateGuard(UserRole.OrganizationAdmin, organizationId: 7);

        Assert.Equal(expected, await guard.CanAccessBeehiveAsync(10));
    }

    [Fact]
    public async Task CanAccessBeehive_MissingBeehive_False()
    {
        _uow.Beehives.GetByIdAsync(10).Returns((Beehive?)null);
        var guard = CreateGuard(UserRole.OrganizationAdmin, organizationId: 7);

        Assert.False(await guard.CanAccessBeehiveAsync(10));
    }

    [Fact]
    public async Task EnsureCanAccessBeehive_Denied_Throws()
    {
        _uow.Users.IsUserAssignedToBeehiveAsync(1, 10).Returns(false);
        var guard = CreateGuard(UserRole.Beekeeper, organizationId: 7);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => guard.EnsureCanAccessBeehiveAsync(10));
    }

    // ── Downgrade lock (SPEC-24) ───────────────────────────────────────────────
    //
    // The lock rides on this guard because everything that reads apiary or hive data already passes
    // through it. These lock the two things that make that safe: the refusal is a 402 and not a 403
    // (so the UI offers an upgrade rather than "not yours"), and the escape hatches stay open.

    private AccessGuard GuardWithLock(IPlanLock planLock, UserRole role = UserRole.OrganizationAdmin) =>
        new(new TestCurrentUser { UserId = 1, Role = role, OrganizationId = 7 }, _uow, planLock);

    private void OwnedHive(int hiveId = 10, int apiaryId = 3)
    {
        _uow.Beehives.GetByIdAsync(hiveId).Returns(new Beehive { Id = hiveId, ApiaryId = apiaryId });
        _uow.Apiaries.GetByIdAsync(apiaryId).Returns(new Apiary { Id = apiaryId, OrganizationId = 7 });
    }

    [Fact]
    public async Task EnsureCanAccessBeehive_LockedByPlan_ThrowsPlanLimit_Not403()
    {
        OwnedHive();
        var guard = GuardWithLock(TestPlanLock.Locking([], [10]));

        // Their own hive, so the role check passes — the refusal must be the payment one.
        await Assert.ThrowsAsync<PlanLimitException>(() => guard.EnsureCanAccessBeehiveAsync(10));
    }

    [Fact]
    public async Task EnsureCanAccessBeehive_LockedByPlan_AllowLocked_Passes()
    {
        OwnedHive();
        var guard = GuardWithLock(TestPlanLock.Locking([], [10]));

        // Deleting a hive and syncing an offline inspection both come through here.
        await guard.EnsureCanAccessBeehiveAsync(10, allowLocked: true);
    }

    [Fact]
    public async Task EnsureCanAccessBeehive_NotYours_StillThrows403_EvenWhenLocked()
    {
        _uow.Beehives.GetByIdAsync(10).Returns(new Beehive { Id = 10, ApiaryId = 3 });
        _uow.Apiaries.GetByIdAsync(3).Returns(new Apiary { Id = 3, OrganizationId = 999 });
        var guard = GuardWithLock(TestPlanLock.Locking([], [10]));

        // An outsider must not learn anything about another organization's plan.
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => guard.EnsureCanAccessBeehiveAsync(10));
    }

    [Fact]
    public async Task CanAccessBeehive_LockedByPlan_False()
    {
        OwnedHive();
        var guard = GuardWithLock(TestPlanLock.Locking([], [10]));

        // The assistant and the scan flow resolve targets through this — a locked hive is unreachable.
        Assert.False(await guard.CanAccessBeehiveAsync(10));
    }

    [Fact]
    public async Task EnsureCanManageApiary_LockedByPlan_ThrowsPlanLimit()
    {
        _uow.Apiaries.GetByIdAsync(3).Returns(new Apiary { Id = 3, OrganizationId = 7 });
        var guard = GuardWithLock(TestPlanLock.Locking([3], []));

        await Assert.ThrowsAsync<PlanLimitException>(() => guard.EnsureCanManageApiaryAsync(3));
        await guard.EnsureCanManageApiaryAsync(3, allowLocked: true);   // the delete path
    }

    [Fact]
    public async Task GetAccessibleBeehives_ExcludesLockedByDefault_AndIncludesThemOnRequest()
    {
        _uow.Beehives.GetByOrganizationAsync(7).Returns(new[]
        {
            new Beehive { Id = 10, ApiaryId = 3 },
            new Beehive { Id = 11, ApiaryId = 3 },
        });
        var guard = GuardWithLock(TestPlanLock.Locking([], [11]));

        Assert.Equal([10], (await guard.GetAccessibleBeehivesAsync()).Select(b => b.Id));
        Assert.Equal([10, 11], (await guard.GetAccessibleBeehivesAsync(includeLocked: true)).Select(b => b.Id));
    }

    // ── Assigned id lookups ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAssignedBeehiveIds_NoUser_Empty()
    {
        var guard = CreateGuard(UserRole.Beekeeper, userId: null);
        Assert.Empty(await guard.GetAssignedBeehiveIdsAsync());
    }

    [Fact]
    public async Task GetAssignedBeehiveIds_DelegatesToRepository()
    {
        _uow.Users.GetAssignedBeehiveIdsAsync(1).Returns([10, 11]);
        var guard = CreateGuard(UserRole.Beekeeper, organizationId: 7);

        var ids = await guard.GetAssignedBeehiveIdsAsync();

        Assert.Equal(new HashSet<int> { 10, 11 }, ids);
    }

    // ── Merged hives are invisible (SPEC-19 §5) ────────────────────────────────

    [Fact]
    public async Task GetAccessibleBeehives_SystemAdmin_AsksForActiveHivesOnly()
    {
        var guard = CreateGuard(UserRole.SystemAdmin);
        _uow.Beehives.GetAllActiveAsync().Returns([new Beehive { Id = 10 }]);

        var hives = await guard.GetAccessibleBeehivesAsync();

        // GetAllAsync would include hives that left their apiary; the active-only method must be used.
        Assert.Single(hives);
        await _uow.Beehives.DidNotReceive().GetAllAsync();
        await _uow.Beehives.Received(1).GetAllActiveAsync();
    }

    [Fact]
    public async Task GetAccessibleBeehives_Beekeeper_FiltersOutMergedHives()
    {
        var guard = CreateGuard(UserRole.Beekeeper, organizationId: 7);
        _uow.Users.GetAssignedBeehiveIdsAsync(1).Returns([10, 11]);

        Expression<Func<Beehive, bool>>? captured = null;
        _uow.Beehives.FindAsync(Arg.Do<Expression<Func<Beehive, bool>>>(p => captured = p)).Returns([]);

        await guard.GetAccessibleBeehivesAsync();

        var predicate = Assert.IsAssignableFrom<Expression<Func<Beehive, bool>>>(captured).Compile();
        Assert.True(predicate(new Beehive { Id = 10 }));
        Assert.False(predicate(new Beehive { Id = 11, MergedIntoBeehiveId = 20 }));
        Assert.False(predicate(new Beehive { Id = 12 })); // not assigned
    }
}
