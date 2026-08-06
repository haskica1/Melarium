using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Auth;
using Melarium.Application.Features.Auth.DTOs;
using Melarium.Application.Features.Invitations;
using Melarium.Application.Features.Notifications;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// New organizations start on a 30-day Pro trial (SPEC-09) — implemented as a pre-set expiring
/// Pro plan, so no new machinery: the computed effective plan falls back to Free after expiry.
/// Someone who arrives through an invitation gets the longer trial instead (SPEC-15), and a broken
/// referral must never cost us the sign-up.
/// </summary>
public class RegistrationTrialTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IInvitationService _invitations = Substitute.For<IInvitationService>();
    private readonly AuthService _service;

    public RegistrationTrialTests()
    {
        var config = Substitute.For<IConfiguration>();
        config["Jwt:Secret"].Returns("unit-test-secret-key-that-is-long-enough-123456");
        config["Jwt:Issuer"].Returns("MelariumTests");
        config["Jwt:Audience"].Returns("MelariumTests");
        config["Plans:Trial:Days"].Returns("30");

        _service = new AuthService(
            _uow,
            config,
            Substitute.For<INotificationService>(),
            Substitute.For<IEmailQueue>(),
            new SessionRevoker(_uow),
            _invitations,
            Substitute.For<ILogger<AuthService>>());
    }

    private async Task<Organization> RegisterAsync(string? referralCode = null)
    {
        User? captured = null;
        _uow.Users.GetByEmailAsync(Arg.Any<string>()).Returns((User?)null);
        _uow.Users.GetByPhoneAsync(Arg.Any<string>()).Returns((User?)null);
        _uow.Users.AddAsync(Arg.Do<User>(u => captured = u)).Returns(ci => ci.Arg<User>());

        await _service.RegisterAsync(new RegisterDto(
            "Asim", "Tester", "new@org.ba", "061 123 456", "Correct123!", "Nova Organizacija", null, referralCode));

        return captured!.Organization!;
    }

    [Fact]
    public async Task Register_CreatesOrganization_OnThirtyDayProTrial()
    {
        _invitations.ResolveTrialDaysAsync(Arg.Is<string?>(c => c == null), Arg.Any<string>()).Returns(30);
        var before = DateTime.UtcNow.Date;

        var org = await RegisterAsync();

        Assert.Equal(PlanType.Pro, org.Plan);
        Assert.Equal("Probni period", org.PlanNotes);
        Assert.NotNull(org.PlanValidUntil);
        Assert.Equal(before.AddDays(30), org.PlanValidUntil!.Value.Date);
    }

    [Fact]
    public async Task Register_ThroughAnInvitation_GetsTheLongerTrial()
    {
        _invitations.ResolveTrialDaysAsync(Arg.Is<string?>(c => c == "abc123"), Arg.Any<string>()).Returns(60);
        var before = DateTime.UtcNow.Date;

        var org = await RegisterAsync("abc123");

        Assert.Equal(before.AddDays(60), org.PlanValidUntil!.Value.Date);
        Assert.Equal(PlanType.Pro, org.Plan);
        // Still the trial as far as the plans page is concerned — the marker string is untouched.
        Assert.Equal("Probni period", org.PlanNotes);
    }

    [Fact]
    public async Task Register_StillSucceeds_WhenAttributionThrows()
    {
        // A bad ?ref= in a link someone pasted into a group chat must never cost us the account.
        _invitations.ResolveTrialDaysAsync(Arg.Any<string?>(), Arg.Any<string>()).Returns(30);
        _invitations
            .TryAttributeRegistrationAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns<Task>(_ => throw new InvalidOperationException("attribution blew up"));

        var org = await RegisterAsync("broken-code");

        Assert.NotNull(org.PlanValidUntil);
        Assert.Equal(PlanType.Pro, org.Plan);
    }
}
