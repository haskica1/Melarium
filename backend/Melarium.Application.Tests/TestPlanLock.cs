using Melarium.Application.Common.Security;
using Melarium.Domain.Common;
using NSubstitute;

namespace Melarium.Application.Tests;

/// <summary>
/// <see cref="IPlanLock"/> stand-ins for tests about something other than the downgrade lock.
/// NSubstitute hands back a completed task carrying <c>null</c> for an unstubbed
/// <c>Task&lt;PlanLockResult&gt;</c>, so the empty sets have to be stubbed explicitly or every
/// caller dereferences null.
/// </summary>
public static class TestPlanLock
{
    /// <summary>A plan that locks nothing — the state every pre-SPEC-24 test assumes.</summary>
    public static IPlanLock Unlocked()
    {
        var planLock = Substitute.For<IPlanLock>();
        planLock.GetForCurrentUserAsync().Returns(PlanLockResult.Empty);
        planLock.GetForOrganizationAsync(Arg.Any<int>()).Returns(PlanLockResult.Empty);
        planLock.PreviewForPlanAsync(Arg.Any<int>(), Arg.Any<Domain.Enums.PlanType>()).Returns(PlanLockResult.Empty);
        return planLock;
    }

    /// <summary>A plan that locks exactly the given ids, and refuses them the way the real one does.</summary>
    public static IPlanLock Locking(int[] apiaryIds, int[] beehiveIds)
    {
        var result = new PlanLockResult(apiaryIds.ToHashSet(), beehiveIds.ToHashSet());

        var planLock = Substitute.For<IPlanLock>();
        planLock.GetForCurrentUserAsync().Returns(result);
        planLock.GetForOrganizationAsync(Arg.Any<int>()).Returns(result);
        planLock.PreviewForPlanAsync(Arg.Any<int>(), Arg.Any<Domain.Enums.PlanType>()).Returns(result);

        planLock.IsApiaryLockedAsync(Arg.Any<int>())
            .Returns(ci => result.ApiaryIds.Contains(ci.Arg<int>()));
        planLock.IsBeehiveLockedAsync(Arg.Any<int>())
            .Returns(ci => result.BeehiveIds.Contains(ci.Arg<int>()));

        planLock.EnsureApiaryUnlockedAsync(Arg.Any<int>())
            .Returns(ci => result.ApiaryIds.Contains(ci.Arg<int>())
                ? Task.FromException(new Common.Exceptions.PlanLimitException("zaključan pčelinjak"))
                : Task.CompletedTask);
        planLock.EnsureBeehiveUnlockedAsync(Arg.Any<int>())
            .Returns(ci => result.BeehiveIds.Contains(ci.Arg<int>())
                ? Task.FromException(new Common.Exceptions.PlanLimitException("zaključana košnica"))
                : Task.CompletedTask);

        return planLock;
    }
}
