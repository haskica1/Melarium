using System.Linq.Expressions;
using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Notifications;
using Melarium.Application.Features.Profile;
using Melarium.Application.Features.Profile.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Deleting your own account. The property copying is not what is worth locking here — these are the
/// four things that would be silent, expensive or unrecoverable if they broke: the organization goes
/// only when its administrator is the last one in it, a member of any other role never takes it down
/// with them, todo assignments are released before the delete (the foreign key that would otherwise
/// crash it), and the whole thing is refused without the right password and the typed organization name.
/// </summary>
public class AccountDeletionTests
{
    private const int UserId = 7;
    private const int OrgId = 5;
    private const string Password = "tajna-lozinka-123";

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ISessionRevoker _sessions = Substitute.For<ISessionRevoker>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    private ProfileService Service(ICurrentUser user) =>
        new(_uow, user, _sessions, _notifications);

    private static TestCurrentUser Caller(UserRole role, int? orgId = OrgId) => new()
    {
        UserId = UserId, Role = role, OrganizationId = orgId,
    };

    /// <summary>
    /// The account under test, with a real BCrypt hash — the password check is the guard being
    /// exercised, so stubbing it away would test nothing.
    /// </summary>
    private User GivenUser(UserRole role, int? orgId = OrgId)
    {
        var user = new User
        {
            Id = UserId,
            FirstName = "Asim",
            LastName = "Pčelar",
            Email = "asim@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
            Role = role,
            OrganizationId = orgId,
        };
        _uow.Users.GetByIdAsync(UserId).Returns(user);
        _uow.Todos.FindAsync(Arg.Any<Expression<Func<Todo, bool>>>()).Returns([]);
        return user;
    }

    private Organization GivenOrganization(int memberCount)
    {
        var org = new Organization { Id = OrgId, Name = "Zlatna košnica" };
        _uow.Organizations.GetByIdAsync(OrgId).Returns(org);
        _uow.Users.CountByOrganizationAsync(OrgId).Returns(memberCount);
        _uow.Apiaries.FindAsync(Arg.Any<Expression<Func<Apiary, bool>>>()).Returns([]);
        _uow.Organizations.GetBeehiveCountsAsync(OrgId).Returns(new Dictionary<int, int> { [OrgId] = 12 });
        return org;
    }

    // ── The password gate ──────────────────────────────────────────────────────

    [Fact]
    public async Task WrongPassword_DeletesNothing()
    {
        GivenUser(UserRole.Beekeeper);
        GivenOrganization(memberCount: 3);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service(Caller(UserRole.Beekeeper)).DeleteMyAccountAsync(new DeleteAccountDto("pogrešna", null)));

        await _uow.Users.DidNotReceive().DeleteAsync(Arg.Any<User>());
        await _uow.Organizations.DidNotReceive().DeleteAsync(Arg.Any<Organization>());
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    // ── Who takes the organization down with them ──────────────────────────────

    [Fact]
    public async Task Beekeeper_DeletesOnlyTheirOwnAccount()
    {
        var user = GivenUser(UserRole.Beekeeper);
        GivenOrganization(memberCount: 3);

        await Service(Caller(UserRole.Beekeeper)).DeleteMyAccountAsync(new DeleteAccountDto(Password, null));

        await _uow.Users.Received(1).DeleteAsync(user);
        await _uow.Organizations.DidNotReceive().DeleteAsync(Arg.Any<Organization>());
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task LoneMemberWhoIsNotTheAdmin_LeavesTheOrganizationStanding()
    {
        // An organization the SystemAdmin set up and still looks after: its records are not this
        // member's to erase, even though they happen to be the only account in it.
        var user = GivenUser(UserRole.Beekeeper);
        GivenOrganization(memberCount: 1);

        await Service(Caller(UserRole.Beekeeper)).DeleteMyAccountAsync(new DeleteAccountDto(Password, null));

        await _uow.Users.Received(1).DeleteAsync(user);
        await _uow.Organizations.DidNotReceive().DeleteAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task OrgAdminWithOtherMembers_IsRefusedAndToldToHandOver()
    {
        GivenUser(UserRole.OrganizationAdmin);
        GivenOrganization(memberCount: 4);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service(Caller(UserRole.OrganizationAdmin))
                .DeleteMyAccountAsync(new DeleteAccountDto(Password, null)));

        Assert.Contains("prenesite vlasništvo", ex.Message);
        await _uow.Users.DidNotReceive().DeleteAsync(Arg.Any<User>());
        await _uow.Organizations.DidNotReceive().DeleteAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task LastOrgAdmin_DeletesTheAccountAndTheOrganization_InOneSave()
    {
        var user = GivenUser(UserRole.OrganizationAdmin);
        var org = GivenOrganization(memberCount: 1);

        await Service(Caller(UserRole.OrganizationAdmin))
            .DeleteMyAccountAsync(new DeleteAccountDto(Password, "Zlatna košnica"));

        await _uow.Users.Received(1).DeleteAsync(user);
        await _uow.Organizations.Received(1).DeleteAsync(org);
        // One save, so a failure cannot leave a deleted user beside an unreachable organization.
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task LastOrgAdmin_WithoutTheTypedOrganizationName_IsRefused()
    {
        GivenUser(UserRole.OrganizationAdmin);
        GivenOrganization(memberCount: 1);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service(Caller(UserRole.OrganizationAdmin))
                .DeleteMyAccountAsync(new DeleteAccountDto(Password, "zlatna kosnica")));

        await _uow.Organizations.DidNotReceive().DeleteAsync(Arg.Any<Organization>());
        await _uow.Users.DidNotReceive().DeleteAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task LastOrgAdmin_AcceptsTheNameWithSurroundingWhitespace()
    {
        var org = GivenOrganization(memberCount: 1);
        GivenUser(UserRole.OrganizationAdmin);

        await Service(Caller(UserRole.OrganizationAdmin))
            .DeleteMyAccountAsync(new DeleteAccountDto(Password, "  Zlatna košnica  "));

        await _uow.Organizations.Received(1).DeleteAsync(org);
    }

    // ── The foreign key that would otherwise crash the delete ──────────────────

    [Fact]
    public async Task AssignedTodos_AreReleasedBeforeTheUserIsDeleted()
    {
        var user = GivenUser(UserRole.Beekeeper);
        GivenOrganization(memberCount: 3);

        var todo = new Todo { Id = 42, AssignedToId = UserId, Title = "Pregled košnice 3" };
        _uow.Todos.FindAsync(Arg.Any<Expression<Func<Todo, bool>>>()).Returns([todo]);

        await Service(Caller(UserRole.Beekeeper)).DeleteMyAccountAsync(new DeleteAccountDto(Password, null));

        // Todo.AssignedToId is the one user foreign key configured NoAction: left set, PostgreSQL
        // refuses the delete outright rather than clearing it.
        Assert.Null(todo.AssignedToId);
        await _uow.Todos.Received(1).UpdateAsync(todo);
        await _uow.Users.Received(1).DeleteAsync(user);
    }

    // ── Platform safety ────────────────────────────────────────────────────────

    [Fact]
    public async Task LastSystemAdmin_CannotDeleteThemselves()
    {
        GivenUser(UserRole.SystemAdmin, orgId: null);
        _uow.Users.CountByRoleAsync(UserRole.SystemAdmin).Returns(1);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service(Caller(UserRole.SystemAdmin, orgId: null))
                .DeleteMyAccountAsync(new DeleteAccountDto(Password, null)));

        await _uow.Users.DidNotReceive().DeleteAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task SystemAdmin_CanLeaveWhenAnotherOneRemains()
    {
        var user = GivenUser(UserRole.SystemAdmin, orgId: null);
        _uow.Users.CountByRoleAsync(UserRole.SystemAdmin).Returns(2);

        await Service(Caller(UserRole.SystemAdmin, orgId: null))
            .DeleteMyAccountAsync(new DeleteAccountDto(Password, null));

        await _uow.Users.Received(1).DeleteAsync(user);
        await _uow.Organizations.DidNotReceive().DeleteAsync(Arg.Any<Organization>());
    }

    // ── The preview the confirmation screen is built from ──────────────────────

    [Fact]
    public async Task Preview_TellsTheOrgAdminWithMembersToHandOverFirst()
    {
        GivenUser(UserRole.OrganizationAdmin);
        GivenOrganization(memberCount: 4);

        var preview = await Service(Caller(UserRole.OrganizationAdmin)).GetDeletionPreviewAsync();

        Assert.Equal("transfer-required", preview.Mode);
        Assert.Equal(4, preview.MemberCount);
        Assert.False(preview.DeletesTreatmentRegister);
    }

    [Fact]
    public async Task Preview_WarnsTheLastOrgAdminAboutTheTreatmentRegister()
    {
        GivenUser(UserRole.OrganizationAdmin);
        GivenOrganization(memberCount: 1);

        var preview = await Service(Caller(UserRole.OrganizationAdmin)).GetDeletionPreviewAsync();

        Assert.Equal("organization", preview.Mode);
        Assert.Equal("Zlatna košnica", preview.OrganizationName);
        Assert.Equal(12, preview.BeehiveCount);
        Assert.True(preview.DeletesTreatmentRegister);
    }

    [Fact]
    public async Task Preview_ForAnOrdinaryMemberIsJustTheirAccount()
    {
        GivenUser(UserRole.ApiaryAdmin);
        GivenOrganization(memberCount: 4);

        var preview = await Service(Caller(UserRole.ApiaryAdmin)).GetDeletionPreviewAsync();

        Assert.Equal("account", preview.Mode);
        Assert.False(preview.DeletesTreatmentRegister);
    }

    [Fact]
    public async Task Preview_AndDeletion_AgreeOnTheMode()
    {
        // The preview drives what the dialog asks for, so a preview that says "account" while the
        // deletion decides "organization" would destroy an organization nobody was warned about.
        GivenUser(UserRole.OrganizationAdmin);
        GivenOrganization(memberCount: 1);
        var service = Service(Caller(UserRole.OrganizationAdmin));

        var preview = await service.GetDeletionPreviewAsync();
        Assert.Equal("organization", preview.Mode);

        // Same state, no organization name → refused, which is the "organization" branch talking.
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.DeleteMyAccountAsync(new DeleteAccountDto(Password, null)));
    }
}
