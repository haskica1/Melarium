using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Models;
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
/// Password reset: the token is single-use and hashed at rest, the request endpoint must not
/// reveal whether an address is registered, and a completed reset has to end every session.
/// </summary>
public class PasswordResetTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IEmailQueue _emailQueue = Substitute.For<IEmailQueue>();
    private readonly AuthService _service;

    public PasswordResetTests()
    {
        var config = Substitute.For<IConfiguration>();
        config["Jwt:Secret"].Returns("unit-test-secret-key-that-is-long-enough-123456");
        config["Jwt:Issuer"].Returns("MelariumTests");
        config["Jwt:Audience"].Returns("MelariumTests");
        config["FrontendUrl"].Returns("https://melarium.app");

        _service = new AuthService(
            _uow, config, Substitute.For<INotificationService>(), _emailQueue, new SessionRevoker(_uow));
    }

    private static User Existing(int id = 1) => new()
    {
        Id           = id,
        FirstName    = "Asim",
        LastName     = "Tester",
        Email        = "asim@test.ba",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword1"),
        Role         = UserRole.OrganizationAdmin,
    };

    // ── Requesting a link ──────────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_UnknownEmail_SucceedsSilently_AndSendsNothing()
    {
        _uow.Users.GetByEmailAsync(Arg.Any<string>()).Returns((User?)null);

        // Must not throw — a different outcome for unknown addresses would leak who has an account.
        await _service.ForgotPasswordAsync("nobody@test.ba");

        _emailQueue.DidNotReceive().Enqueue(Arg.Any<QueuedEmail>());
        await _uow.UserTokens.DidNotReceive().AddAsync(Arg.Any<UserToken>());
    }

    [Fact]
    public async Task ForgotPassword_KnownEmail_StoresHashedToken_AndEmailsRawLink()
    {
        UserToken? persisted = null;
        QueuedEmail? sent = null;

        _uow.Users.GetByEmailAsync("asim@test.ba").Returns(Existing());
        _uow.UserTokens.GetActiveByUserAsync(1, UserTokenPurpose.PasswordReset).Returns([]);
        _uow.UserTokens.AddAsync(Arg.Do<UserToken>(t => persisted = t)).Returns(ci => ci.Arg<UserToken>());
        _emailQueue.Enqueue(Arg.Do<QueuedEmail>(e => sent = e));

        await _service.ForgotPasswordAsync("  Asim@Test.BA ");

        Assert.NotNull(persisted);
        Assert.Equal(UserTokenPurpose.PasswordReset, persisted!.Purpose);
        Assert.True(persisted.ExpiresAt > DateTime.UtcNow);
        Assert.Null(persisted.UsedAt);

        Assert.NotNull(sent);
        Assert.StartsWith("https://melarium.app/reset-password?token=", sent!.ActionUrl);

        // The raw token travels in the email only; the database keeps its hash.
        var rawToken = sent.ActionUrl!.Split("token=")[1];
        Assert.DoesNotContain(rawToken, persisted.TokenHash);
    }

    [Fact]
    public async Task ForgotPassword_InvalidatesEarlierOutstandingLinks()
    {
        var earlier = new UserToken { Id = 9, UserId = 1, Purpose = UserTokenPurpose.PasswordReset };
        _uow.Users.GetByEmailAsync("asim@test.ba").Returns(Existing());
        _uow.UserTokens.GetActiveByUserAsync(1, UserTokenPurpose.PasswordReset).Returns([earlier]);

        await _service.ForgotPasswordAsync("asim@test.ba");

        Assert.NotNull(earlier.UsedAt);
    }

    // ── Redeeming a link ───────────────────────────────────────────────────────

    private UserToken ArrangeValidToken(string raw, User user)
    {
        var token = new UserToken
        {
            Id        = 1,
            UserId    = user.Id,
            Purpose   = UserTokenPurpose.PasswordReset,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
        };
        _uow.UserTokens.GetByHashAsync(Arg.Any<string>(), UserTokenPurpose.PasswordReset).Returns(token);
        _uow.Users.GetByIdAsync(user.Id).Returns(user);
        _ = raw;
        return token;
    }

    [Fact]
    public async Task ResetPassword_SetsNewPassword_MarksTokenUsed_AndRevokesEverySession()
    {
        var user = Existing();
        var token = ArrangeValidToken("raw", user);
        var liveSession = new RefreshToken { Id = 3, UserId = user.Id, ExpiresAt = DateTime.UtcNow.AddDays(7) };
        _uow.RefreshTokens.GetActiveByUserAsync(user.Id).Returns([liveSession]);

        await _service.ResetPasswordAsync(new ResetPasswordDto("raw", "BrandNewPass1"));

        Assert.True(BCrypt.Net.BCrypt.Verify("BrandNewPass1", user.PasswordHash));
        Assert.NotNull(token.UsedAt);
        // Whoever forced the reset must not keep a working refresh token.
        Assert.NotNull(liveSession.RevokedAt);
    }

    [Fact]
    public async Task ResetPassword_AlsoMarksTheAddressVerified()
    {
        var user = Existing();
        user.EmailVerifiedAt = null;
        ArrangeValidToken("raw", user);
        _uow.RefreshTokens.GetActiveByUserAsync(user.Id).Returns([]);

        await _service.ResetPasswordAsync(new ResetPasswordDto("raw", "BrandNewPass1"));

        // Receiving the reset mail is itself proof the mailbox belongs to them.
        Assert.NotNull(user.EmailVerifiedAt);
    }

    [Fact]
    public async Task ResetPassword_RejectsAnAlreadyUsedToken()
    {
        var user = Existing();
        var token = ArrangeValidToken("raw", user);
        token.UsedAt = DateTime.UtcNow.AddMinutes(-1);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.ResetPasswordAsync(new ResetPasswordDto("raw", "BrandNewPass1")));
    }

    [Fact]
    public async Task ResetPassword_RejectsAnExpiredToken()
    {
        var user = Existing();
        var token = ArrangeValidToken("raw", user);
        token.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.ResetPasswordAsync(new ResetPasswordDto("raw", "BrandNewPass1")));
    }

    [Fact]
    public async Task ResetPassword_RejectsAnUnknownToken()
    {
        _uow.UserTokens.GetByHashAsync(Arg.Any<string>(), UserTokenPurpose.PasswordReset)
            .Returns((UserToken?)null);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.ResetPasswordAsync(new ResetPasswordDto("bogus", "BrandNewPass1")));
    }

    [Fact]
    public async Task ResetPassword_WillNotAcceptAVerificationToken()
    {
        // The purpose is part of the lookup, so a verification link cannot reset a password.
        _uow.UserTokens.GetByHashAsync(Arg.Any<string>(), UserTokenPurpose.PasswordReset)
            .Returns((UserToken?)null);
        _uow.UserTokens.GetByHashAsync(Arg.Any<string>(), UserTokenPurpose.EmailVerification)
            .Returns(new UserToken { Id = 2, UserId = 1, Purpose = UserTokenPurpose.EmailVerification });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.ResetPasswordAsync(new ResetPasswordDto("verification-token", "BrandNewPass1")));
    }
}
