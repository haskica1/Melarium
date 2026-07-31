using Melarium.Application.Common.Validation;
using Melarium.Application.Features.Admin.DTOs;
using Melarium.Application.Features.Admin.Validators;
using Melarium.Application.Features.Auth.DTOs;
using Melarium.Application.Features.Auth.Validators;
using Melarium.Application.Features.OrgManagement.DTOs;
using Melarium.Application.Features.OrgManagement.Validators;
using Melarium.Application.Features.Profile.DTOs;
using Melarium.Application.Features.Profile.Validators;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// The password policy must hold at *every* entry point that sets a password. It previously
/// existed only on self-registration: admin-created users, organisation members and the profile
/// password change accepted a single character.
/// </summary>
public class PasswordPolicyTests
{
    private const string TooShort = "Abc123!";              // 7 chars — one below the minimum
    private const string Valid    = "Abc123!xyz";

    // ── Self-registration (was already covered — guards against regression) ──────

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData(TooShort)]
    public void Register_rejects_passwords_below_the_minimum(string password)
    {
        var result = new RegisterValidator().Validate(new RegisterDto(
            "Ime", "Prezime", "user@example.com", "061 123 456", password, "Organizacija", null));

        Assert.False(result.IsValid);
    }

    // ── Admin-created users ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData(TooShort)]
    public void CreateAdminUser_rejects_passwords_below_the_minimum(string password)
    {
        var result = new CreateAdminUserValidator().Validate(new CreateAdminUserDto
        {
            FirstName = "Ime",
            LastName  = "Prezime",
            Email     = "user@example.com",
            Phone     = "061 123 456",
            Password  =password,
            Role      = "Beekeeper",
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void CreateAdminUser_accepts_a_conforming_password()
    {
        var result = new CreateAdminUserValidator().Validate(new CreateAdminUserDto
        {
            FirstName = "Ime",
            LastName  = "Prezime",
            Email     = "user@example.com",
            Phone     = "061 123 456",
            Password  =Valid,
            Role      = "Beekeeper",
        });

        Assert.True(result.IsValid);
    }

    // ── Organisation members ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData(TooShort)]
    public void CreateOrgMember_rejects_passwords_below_the_minimum(string password)
    {
        var result = new CreateOrgMemberValidator().Validate(new CreateOrgMemberDto
        {
            FirstName = "Ime",
            LastName  = "Prezime",
            Email     = "member@example.com",
            Phone     = "061 123 456",
            Password  =password,
            Role      = "Beekeeper",
        });

        Assert.False(result.IsValid);
    }

    // ── Profile password change ──────────────────────────────────────────────────

    [Theory]
    [InlineData("a")]
    [InlineData(TooShort)]
    public void UpdateProfile_rejects_a_new_password_below_the_minimum(string newPassword)
    {
        var result = new UpdateProfileValidator().Validate(
            new UpdateProfileDto("Ime", "Prezime", "user@example.com", null, "CurrentPass1", newPassword));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void UpdateProfile_requires_the_current_password_when_setting_a_new_one()
    {
        var result = new UpdateProfileValidator().Validate(
            new UpdateProfileDto("Ime", "Prezime", "user@example.com", null, null, Valid));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void UpdateProfile_allows_editing_the_name_without_touching_the_password()
    {
        var result = new UpdateProfileValidator().Validate(
            new UpdateProfileDto("Ime", "Prezime", "user@example.com", null, null, null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateProfile_accepts_a_conforming_password_change()
    {
        var result = new UpdateProfileValidator().Validate(
            new UpdateProfileDto("Ime", "Prezime", "user@example.com", null, "CurrentPass1", Valid));

        Assert.True(result.IsValid);
    }

    // ── Shared free-text bounds (previously unbounded on these endpoints) ─────────

    [Fact]
    public void UpdateProfile_rejects_a_malformed_email()
    {
        var result = new UpdateProfileValidator().Validate(
            new UpdateProfileDto("Ime", "Prezime", "not-an-email", null, null, null));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void UpdateAdminUser_rejects_an_over_long_name()
    {
        var result = new UpdateAdminUserValidator().Validate(new UpdateAdminUserDto
        {
            FirstName = new string('a', 101),
            LastName  = "Prezime",
            Email     = "user@example.com",
            Role      = "Beekeeper",
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void The_minimum_is_the_one_declared_by_the_shared_policy()
    {
        // Guards against the validators drifting apart from PasswordRules.
        Assert.Equal(8, PasswordRules.MinLength);
    }
}
