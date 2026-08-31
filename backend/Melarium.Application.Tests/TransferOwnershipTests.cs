using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Notifications;
using Melarium.Application.Features.OrgManagement;
using Melarium.Application.Features.OrgManagement.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Handing an organization over to one of its members. This exists because account deletion needs an
/// exit for the administrator of an organization that still has people in it — without it, that
/// person can never delete their account, which is itself a store rejection.
///
/// What is locked here: both roles actually swap, neither account is left in a state the
/// role/apiary consistency rule rejects, both sessions end (role is a JWT claim), and a member id
/// from another organization is indistinguishable from one that does not exist.
/// </summary>
public class TransferOwnershipTests
{
    private const int OrgId = 5;
    private const int OutgoingId = 1;
    private const int SuccessorId = 2;

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly IPlanGuard _plan = Substitute.For<IPlanGuard>();
    private readonly ISessionRevoker _sessions = Substitute.For<ISessionRevoker>();

    private OrgManagementService Service(ICurrentUser user) =>
        new(_uow, _notifications, user, _plan, _sessions);

    private static TestCurrentUser OrgAdmin => new()
    {
        UserId = OutgoingId, Role = UserRole.OrganizationAdmin, OrganizationId = OrgId,
    };

    private (User Outgoing, User Successor) GivenBoth(int? successorOrgId = OrgId)
    {
        var outgoing = new User
        {
            Id = OutgoingId, FirstName = "Asim", LastName = "Haskić",
            Role = UserRole.OrganizationAdmin, OrganizationId = OrgId,
        };
        var successor = new User
        {
            Id = SuccessorId, FirstName = "Emir", LastName = "Pčelar",
            // An ApiaryAdmin on purpose: the apiary scoping has to be cleared on the way up, or the
            // new owner is an OrganizationAdmin pinned to a single apiary — a state the consistency
            // rule rejects outright.
            Role = UserRole.ApiaryAdmin, OrganizationId = successorOrgId, ApiaryId = 33,
        };

        _uow.Users.GetByIdAsync(OutgoingId).Returns(outgoing);
        _uow.Users.GetByIdAsync(SuccessorId).Returns(successor);
        _uow.Organizations.GetByIdAsync(OrgId).Returns(new Organization { Id = OrgId, Name = "Zlatna košnica" });

        return (outgoing, successor);
    }

    [Fact]
    public async Task Transfer_SwapsBothRoles()
    {
        var (outgoing, successor) = GivenBoth();

        await Service(OrgAdmin).TransferOwnershipAsync(new TransferOwnershipDto(SuccessorId));

        Assert.Equal(UserRole.OrganizationAdmin, successor.Role);
        Assert.Equal(UserRole.Beekeeper, outgoing.Role);
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Transfer_ClearsApiaryScopingOnBothAccounts()
    {
        var (outgoing, successor) = GivenBoth();

        await Service(OrgAdmin).TransferOwnershipAsync(new TransferOwnershipDto(SuccessorId));

        // Only an ApiaryAdmin may carry an ApiaryId; neither account is one after this.
        Assert.Null(successor.ApiaryId);
        Assert.Null(outgoing.ApiaryId);
    }

    [Fact]
    public async Task Transfer_EndsBothSessions()
    {
        GivenBoth();

        await Service(OrgAdmin).TransferOwnershipAsync(new TransferOwnershipDto(SuccessorId));

        // Role, organization and apiary are JWT claims — both tokens now describe permissions their
        // owner no longer has.
        await _sessions.Received(1).RevokeAllAsync(SuccessorId);
        await _sessions.Received(1).RevokeAllAsync(OutgoingId);
    }

    [Fact]
    public async Task Transfer_TellsTheSuccessorTheyNowRunTheOrganization()
    {
        GivenBoth();

        await Service(OrgAdmin).TransferOwnershipAsync(new TransferOwnershipDto(SuccessorId));

        await _notifications.Received(1).NotifyAsync(
            SuccessorId,
            Arg.Any<string>(),
            Arg.Is<string>(m => m.Contains("Zlatna košnica")),
            NotificationType.OrganizationOwnershipTransferred,
            Arg.Any<int?>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task Transfer_ToAMemberOfAnotherOrganization_LooksLikeANonExistentUser()
    {
        GivenBoth(successorOrgId: 999);

        // NotFound rather than Forbidden on purpose: otherwise the endpoint answers
        // "does user 2 exist?" for anyone who asks.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Service(OrgAdmin).TransferOwnershipAsync(new TransferOwnershipDto(SuccessorId)));

        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Transfer_ToYourself_IsRefused()
    {
        GivenBoth();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service(OrgAdmin).TransferOwnershipAsync(new TransferOwnershipDto(OutgoingId)));

        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Transfer_ByANonAdminMember_IsRefused()
    {
        GivenBoth();
        var apiaryAdmin = new TestCurrentUser
        {
            UserId = OutgoingId, Role = UserRole.ApiaryAdmin, OrganizationId = OrgId,
        };

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            Service(apiaryAdmin).TransferOwnershipAsync(new TransferOwnershipDto(SuccessorId)));

        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Transfer_NotifiesOnlyAfterTheRoleChangeIsCommitted()
    {
        GivenBoth();

        await Service(OrgAdmin).TransferOwnershipAsync(new TransferOwnershipDto(SuccessorId));

        // NotifyAsync runs its own SaveChanges on the shared DbContext. Notifying first would push
        // the role change out early and, on failure, take it down with it.
        Received.InOrder(() =>
        {
            _uow.SaveChangesAsync();
            _notifications.NotifyAsync(
                SuccessorId, Arg.Any<string>(), Arg.Any<string>(),
                NotificationType.OrganizationOwnershipTransferred,
                Arg.Any<int?>(), Arg.Any<string?>());
        });
    }
}
