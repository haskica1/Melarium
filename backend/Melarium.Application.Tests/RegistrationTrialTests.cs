using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Auth;
using Melarium.Application.Features.Auth.DTOs;
using Melarium.Application.Features.Notifications;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// New organizations start on a 30-day Pro trial (SPEC-09) — implemented as a pre-set expiring
/// Pro plan, so no new machinery: the computed effective plan falls back to Free after expiry.
/// </summary>
public class RegistrationTrialTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
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
            new SessionRevoker(_uow));
    }

    [Fact]
    public async Task Register_CreatesOrganization_OnThirtyDayProTrial()
    {
        User? captured = null;
        _uow.Users.GetByEmailAsync(Arg.Any<string>()).Returns((User?)null);
        _uow.Users.GetByPhoneAsync(Arg.Any<string>()).Returns((User?)null);
        _uow.Users.AddAsync(Arg.Do<User>(u => captured = u)).Returns(ci => ci.Arg<User>());

        var before = DateTime.UtcNow.Date;
        await _service.RegisterAsync(new RegisterDto(
            "Asim", "Tester", "new@org.ba", "061 123 456", "Correct123!", "Nova Organizacija", null));

        var org = captured!.Organization!;
        Assert.Equal(PlanType.Pro, org.Plan);
        Assert.Equal("Probni period", org.PlanNotes);
        Assert.NotNull(org.PlanValidUntil);
        Assert.Equal(before.AddDays(30), org.PlanValidUntil!.Value.Date);
    }
}
