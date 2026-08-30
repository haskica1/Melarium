using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Admin;
using Melarium.Application.Features.Admin.DTOs;
using Melarium.Application.Features.Notifications;
using Melarium.Application.Features.OrgManagement;
using Melarium.Application.Features.OrgManagement.DTOs;
using Melarium.Application.Features.Profile;
using Melarium.Application.Features.Profile.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// A phone number is a login identifier, so two accounts can never hold the same one — the DB's
/// partial unique index is the backstop, and these lock the friendly 422 that has to fire first at
/// every entry point that can set a number. Also locks the two rules that are easy to get wrong:
/// re-saving your *own* number is not a conflict, and a blank field means "leave it alone".
/// </summary>
public class PhoneUniquenessTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private static User Existing(int id = 1, string? phone = "+38761123456") => new()
    {
        Id             = id,
        FirstName      = "Asim",
        LastName       = "Tester",
        Email          = "asim@test.ba",
        Phone          = phone,
        PasswordHash   = BCrypt.Net.BCrypt.HashPassword("Correct123!"),
        Role           = UserRole.Beekeeper,
        OrganizationId = 5,
    };

    private ProfileService Profile(int callerId = 1) => new(
        _uow,
        new TestCurrentUser { UserId = callerId, Role = UserRole.Beekeeper, OrganizationId = 5 },
        new SessionRevoker(_uow),
        Substitute.For<INotificationService>());

    private AdminService Admin()
    {
        // Mapping a user now reads "zadnja prijava" off the repository; an unstubbed NSubstitute
        // call hands back null, which is not a shape the real repository can return.
        _uow.Users.GetLastLoginAtAsync(Arg.Any<int?>()).Returns(new Dictionary<int, DateTime>());
        return new AdminService(
            _uow, Substitute.For<INotificationService>(), new SessionRevoker(_uow), Substitute.For<IFileStorage>());
    }

    private static UpdateProfileDto ProfileDto(string? phone) =>
        new("Asim", "Tester", "asim@test.ba", phone, null, null);

    private static UpdateAdminUserDto AdminDto(string? phone) => new()
    {
        FirstName      = "Asim",
        LastName       = "Tester",
        Email          = "asim@test.ba",
        Phone          = phone,
        Role           = nameof(UserRole.Beekeeper),
        OrganizationId = 5,
    };

    // ── Profile (self-service) ─────────────────────────────────────────────────

    [Fact]
    public async Task Profile_ChangingToANumberAnotherAccountHolds_IsRejected()
    {
        var user = Existing();
        _uow.Users.GetByIdAsync(1).Returns(user);
        _uow.Users.IsPhoneTakenAsync("+38762999888", 1).Returns(true);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Profile().UpdateProfileAsync(ProfileDto("062 999 888")));

        Assert.Equal("+38761123456", user.Phone);   // unchanged
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Profile_ChangingToAFreeNumber_IsAccepted()
    {
        var user = Existing();
        _uow.Users.GetByIdAsync(1).Returns(user);
        _uow.Users.IsPhoneTakenAsync("+38762999888", 1).Returns(false);

        var result = await Profile().UpdateProfileAsync(ProfileDto("062 999 888"));

        Assert.Equal("+38762999888", user.Phone);
        Assert.Equal("+38762999888", result.Phone);
    }

    // Without excluding the caller's own id, saving any other profile field would fail for
    // everyone who already has a number.
    [Fact]
    public async Task Profile_ResavingYourOwnNumber_IsNotAConflict()
    {
        var user = Existing();
        _uow.Users.GetByIdAsync(1).Returns(user);
        // Anyone else asking about this number would be told it is taken.
        _uow.Users.IsPhoneTakenAsync("+38761123456", Arg.Any<int?>()).Returns(true);
        _uow.Users.IsPhoneTakenAsync("+38761123456", 1).Returns(false);

        var result = await Profile().UpdateProfileAsync(ProfileDto("061 123 456"));

        Assert.Equal("+38761123456", result.Phone);
    }

    // The stored value is canonical, so re-submitting the same number written differently must not
    // even register as a change.
    [Fact]
    public async Task Profile_AnotherSpellingOfYourOwnNumber_IsNotAChange()
    {
        var user = Existing();
        _uow.Users.GetByIdAsync(1).Returns(user);

        await Profile().UpdateProfileAsync(ProfileDto("+387 61 123 456"));

        Assert.Equal("+38761123456", user.Phone);
        await _uow.Users.DidNotReceive().IsPhoneTakenAsync(Arg.Any<string>(), Arg.Any<int?>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Profile_BlankPhone_LeavesTheStoredNumberUntouched(string? blank)
    {
        var user = Existing();
        _uow.Users.GetByIdAsync(1).Returns(user);

        var result = await Profile().UpdateProfileAsync(ProfileDto(blank));

        Assert.Equal("+38761123456", user.Phone);
        Assert.Equal("+38761123456", result.Phone);
    }

    // Accounts that predate the field must stay editable without inventing a number for them.
    [Fact]
    public async Task Profile_AccountWithNoNumber_CanStillSaveOtherFields()
    {
        var user = Existing(phone: null);
        _uow.Users.GetByIdAsync(1).Returns(user);

        var result = await Profile().UpdateProfileAsync(ProfileDto(null));

        Assert.Null(user.Phone);
        Assert.Null(result.Phone);
    }

    // ── Admin edit of someone else's account ───────────────────────────────────

    [Fact]
    public async Task AdminUpdate_ChangingToANumberAnotherAccountHolds_IsRejected()
    {
        var user = Existing();
        _uow.Users.GetByIdWithAssignedBeehivesAsync(1).Returns(user);
        _uow.Organizations.ExistsAsync(5).Returns(true);
        _uow.Users.IsPhoneTakenAsync("+38762999888", 1).Returns(true);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Admin().UpdateUserAsync(1, AdminDto("062 999 888")));

        Assert.Equal("+38761123456", user.Phone);
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task AdminUpdate_ChangingToAFreeNumber_IsAccepted()
    {
        var user = Existing();
        _uow.Users.GetByIdWithAssignedBeehivesAsync(1).Returns(user);
        _uow.Organizations.ExistsAsync(5).Returns(true);
        _uow.Users.IsPhoneTakenAsync("+38762999888", 1).Returns(false);

        await Admin().UpdateUserAsync(1, AdminDto("062 999 888"));

        Assert.Equal("+38762999888", user.Phone);
    }

    // A client that doesn't send the field must not silently strip the account's second identifier.
    [Fact]
    public async Task AdminUpdate_BlankPhone_LeavesTheStoredNumberUntouched()
    {
        var user = Existing();
        _uow.Users.GetByIdWithAssignedBeehivesAsync(1).Returns(user);
        _uow.Organizations.ExistsAsync(5).Returns(true);

        await Admin().UpdateUserAsync(1, AdminDto(null));

        Assert.Equal("+38761123456", user.Phone);
    }

    // ── Creation ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task AdminCreate_NumberAlreadyRegistered_IsRejected()
    {
        _uow.Users.GetByEmailAsync(Arg.Any<string>()).Returns((User?)null);
        _uow.Users.IsPhoneTakenAsync("+38761123456", null).Returns(true);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Admin().CreateUserAsync(new CreateAdminUserDto
        {
            FirstName      = "Novi",
            LastName       = "Korisnik",
            Email          = "novi@test.ba",
            Phone          = "061 123 456",
            Password       = "Correct123!",
            Role           = nameof(UserRole.Beekeeper),
            OrganizationId = 5,
        }));

        await _uow.Users.DidNotReceive().AddAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task AdminCreate_StoresTheNumberCanonically()
    {
        User? captured = null;
        _uow.Users.GetByEmailAsync(Arg.Any<string>()).Returns((User?)null);
        _uow.Users.IsPhoneTakenAsync(Arg.Any<string>(), Arg.Any<int?>()).Returns(false);
        _uow.Organizations.ExistsAsync(5).Returns(true);
        _uow.Users.AddAsync(Arg.Do<User>(u => captured = u)).Returns(ci => ci.Arg<User>());
        // CreateUserAsync re-reads the row it just inserted before mapping the response.
        _uow.Users.GetByIdWithAssignedBeehivesAsync(Arg.Any<int>()).Returns(_ => captured);

        var dto = await Admin().CreateUserAsync(new CreateAdminUserDto
        {
            FirstName      = "Novi",
            LastName       = "Korisnik",
            Email          = "novi@test.ba",
            Phone          = "061 123 456",
            Password       = "Correct123!",
            Role           = nameof(UserRole.Beekeeper),
            OrganizationId = 5,
        });

        Assert.Equal("+38761123456", captured!.Phone);
        Assert.Equal("+38761123456", dto.Phone);   // and it comes back out on the response
    }

    [Fact]
    public async Task OrgMemberCreate_NumberAlreadyRegistered_IsRejected()
    {
        var service = new OrgManagementService(
            _uow,
            Substitute.For<INotificationService>(),
            new TestCurrentUser { UserId = 9, Role = UserRole.OrganizationAdmin, OrganizationId = 5 },
            Substitute.For<IPlanGuard>(),
            new SessionRevoker(_uow));

        _uow.Users.GetByEmailAsync(Arg.Any<string>()).Returns((User?)null);
        _uow.Users.IsPhoneTakenAsync("+38761123456", null).Returns(true);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateMemberAsync(new CreateOrgMemberDto
        {
            FirstName = "Novi",
            LastName  = "Član",
            Email     = "clan@test.ba",
            Phone     = "061 123 456",
            Password  = "Correct123!",
            Role      = nameof(UserRole.Beekeeper),
        }));

        await _uow.Users.DidNotReceive().AddAsync(Arg.Any<User>());
    }
}
