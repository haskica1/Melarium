using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Models;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Auth;
using Melarium.Application.Features.Notifications;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Email verification is deliberately soft: it records that an address was confirmed but never
/// blocks sign-in. Clicking a link twice must look like success, not an error.
/// </summary>
public class EmailVerificationTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IEmailQueue _emailQueue = Substitute.For<IEmailQueue>();
    private readonly AuthService _service;

    public EmailVerificationTests()
    {
        var config = Substitute.For<IConfiguration>();
        config["Jwt:Secret"].Returns("unit-test-secret-key-that-is-long-enough-123456");
        config["Jwt:Issuer"].Returns("MelariumTests");
        config["Jwt:Audience"].Returns("MelariumTests");
        config["FrontendUrl"].Returns("https://melarium.app");

        _service = new AuthService(
            _uow, config, Substitute.For<INotificationService>(), _emailQueue, new SessionRevoker(_uow));
    }

    private static User Unverified(int id = 1) => new()
    {
        Id              = id,
        FirstName       = "Asim",
        LastName        = "Tester",
        Email           = "asim@test.ba",
        PasswordHash    = BCrypt.Net.BCrypt.HashPassword("OldPassword1"),
        Role            = UserRole.OrganizationAdmin,
        EmailVerifiedAt = null,
    };

    [Fact]
    public async Task VerifyEmail_MarksTheUserVerified_AndBurnsTheToken()
    {
        var user = Unverified();
        var token = new UserToken
        {
            Id = 1, UserId = 1, Purpose = UserTokenPurpose.EmailVerification,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
        };
        _uow.UserTokens.GetByHashAsync(Arg.Any<string>(), UserTokenPurpose.EmailVerification).Returns(token);
        _uow.Users.GetByIdAsync(1).Returns(user);

        await _service.VerifyEmailAsync("raw");

        Assert.NotNull(user.EmailVerifiedAt);
        Assert.NotNull(token.UsedAt);
    }

    [Fact]
    public async Task VerifyEmail_IsIdempotent_WhenAlreadyVerified()
    {
        var user = Unverified();
        user.EmailVerifiedAt = DateTime.UtcNow.AddDays(-1);
        var spentToken = new UserToken
        {
            Id = 1, UserId = 1, Purpose = UserTokenPurpose.EmailVerification,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            UsedAt = DateTime.UtcNow.AddDays(-1),
        };
        _uow.UserTokens.GetByHashAsync(Arg.Any<string>(), UserTokenPurpose.EmailVerification).Returns(spentToken);
        _uow.Users.GetByIdAsync(1).Returns(user);

        // Re-opening the link (or a mail client prefetching it) must not read as a failure.
        await _service.VerifyEmailAsync("raw");
    }

    [Fact]
    public async Task VerifyEmail_RejectsAnExpiredToken()
    {
        var user = Unverified();
        var token = new UserToken
        {
            Id = 1, UserId = 1, Purpose = UserTokenPurpose.EmailVerification,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
        };
        _uow.UserTokens.GetByHashAsync(Arg.Any<string>(), UserTokenPurpose.EmailVerification).Returns(token);
        _uow.Users.GetByIdAsync(1).Returns(user);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.VerifyEmailAsync("raw"));
        Assert.Null(user.EmailVerifiedAt);
    }

    [Fact]
    public async Task VerifyEmail_RejectsAnUnknownToken()
    {
        _uow.UserTokens.GetByHashAsync(Arg.Any<string>(), UserTokenPurpose.EmailVerification)
            .Returns((UserToken?)null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.VerifyEmailAsync("bogus"));
    }

    [Fact]
    public async Task ResendVerification_DoesNothingWhenAlreadyVerified()
    {
        var user = Unverified();
        user.EmailVerifiedAt = DateTime.UtcNow;
        _uow.Users.GetByIdAsync(1).Returns(user);

        await _service.ResendVerificationEmailAsync(1);

        _emailQueue.DidNotReceive().Enqueue(Arg.Any<QueuedEmail>());
    }

    [Fact]
    public async Task ResendVerification_SendsAFreshLinkForAnUnverifiedUser()
    {
        QueuedEmail? sent = null;
        _uow.Users.GetByIdAsync(1).Returns(Unverified());
        _uow.UserTokens.GetActiveByUserAsync(1, UserTokenPurpose.EmailVerification).Returns([]);
        _emailQueue.Enqueue(Arg.Do<QueuedEmail>(e => sent = e));

        await _service.ResendVerificationEmailAsync(1);

        Assert.NotNull(sent);
        Assert.StartsWith("https://melarium.app/verify-email?token=", sent!.ActionUrl);
    }
}
