using Melarium.Application.Common.Exceptions;
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
/// Locks the refresh-token rotation contract: tokens are stored hashed, rotation revokes
/// the presented token, and reuse of a rotated token revokes the user's whole active set.
/// </summary>
public class AuthServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        var config = Substitute.For<IConfiguration>();
        config["Jwt:Secret"].Returns("unit-test-secret-key-that-is-long-enough-123456");
        config["Jwt:Issuer"].Returns("MelariumTests");
        config["Jwt:Audience"].Returns("MelariumTests");

        _service = new AuthService(
            _uow,
            config,
            Substitute.For<INotificationService>(),
            Substitute.For<IEmailQueue>(),
            new SessionRevoker(_uow),
            Substitute.For<IInvitationService>(),
            Substitute.For<ILogger<AuthService>>());
    }

    private static User OrgAdmin(int id = 1) => new()
    {
        Id             = id,
        FirstName      = "Asim",
        LastName       = "Tester",
        Email          = "asim@test.ba",
        Phone          = "+38761123456",
        PasswordHash   = BCrypt.Net.BCrypt.HashPassword("Correct123!"),
        Role           = UserRole.OrganizationAdmin,
        OrganizationId = 5,
        Organization   = new Organization { Id = 5, Name = "TestOrg" },
    };

    // ── Login ──────────────────────────────────────────────────────────────────

    // Failed credentials are 401 (UnauthorizedException), not 422 — and both branches must fail
    // with the *same* exception and message so the response cannot distinguish "no such account"
    // from "wrong password".
    [Fact]
    public async Task Login_UnknownEmail_ThrowsGenericCredentialError()
    {
        _uow.Users.GetByEmailAsync("asim@test.ba").Returns((User?)null);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _service.LoginAsync(new LoginDto("asim@test.ba", "x")));
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsGenericCredentialError()
    {
        _uow.Users.GetByEmailAsync("asim@test.ba").Returns(OrgAdmin());

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _service.LoginAsync(new LoginDto("asim@test.ba", "Wrong123!")));
    }

    [Fact]
    public async Task Login_UnknownEmailAndWrongPassword_AreIndistinguishable()
    {
        _uow.Users.GetByEmailAsync("ghost@test.ba").Returns((User?)null);
        _uow.Users.GetByEmailAsync("asim@test.ba").Returns(OrgAdmin());

        var unknown = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _service.LoginAsync(new LoginDto("ghost@test.ba", "Whatever1!")));
        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _service.LoginAsync(new LoginDto("asim@test.ba", "Whatever1!")));

        Assert.Equal(unknown.Message, wrongPassword.Message);
    }

    [Fact]
    public async Task Login_NormalizesEmail_AndIssuesTokens()
    {
        RefreshToken? persisted = null;
        _uow.Users.GetByEmailAsync("asim@test.ba").Returns(OrgAdmin());
        _uow.RefreshTokens.AddAsync(Arg.Do<RefreshToken>(t => persisted = t)).Returns(ci => ci.Arg<RefreshToken>());

        var result = await _service.LoginAsync(new LoginDto("  Asim@Test.BA ", "Correct123!"));

        Assert.Equal(3, result.Token.Split('.').Length);              // JWT: header.payload.signature
        Assert.Equal(64, result.RefreshToken.Length);                  // 32 random bytes, hex-encoded
        Assert.NotNull(persisted);
        Assert.NotEqual(result.RefreshToken, persisted!.TokenHash);    // only the hash is stored
        Assert.Equal("TestOrg", result.OrganizationName);
    }

    // ── Login by phone ─────────────────────────────────────────────────────────

    // The whole point of accepting a phone number: however the owner writes it down, it has to
    // reach the one canonical value stored on the account.
    [Theory]
    [InlineData("061123456")]
    [InlineData("061 123 456")]
    [InlineData("061-123-456")]
    [InlineData("+38761123456")]
    [InlineData("+387 61 123 456")]
    [InlineData("0038761123456")]
    [InlineData("38761123456")]
    [InlineData("  061 123 456  ")]
    public async Task Login_ByPhone_AcceptsEveryWrittenFormOfTheSameNumber(string typed)
    {
        _uow.Users.GetByPhoneAsync("+38761123456").Returns(OrgAdmin());
        _uow.RefreshTokens.AddAsync(Arg.Any<RefreshToken>()).Returns(ci => ci.Arg<RefreshToken>());

        var result = await _service.LoginAsync(new LoginDto(typed, "Correct123!"));

        Assert.Equal(3, result.Token.Split('.').Length);
        Assert.Equal("TestOrg", result.OrganizationName);
    }

    // A phone that belongs to nobody, and a number too malformed to even look up, must fail
    // exactly like a wrong password — otherwise the endpoint tells you which numbers are registered.
    [Theory]
    [InlineData("062999888")]
    [InlineData("nonsense")]
    public async Task Login_UnknownOrMalformedPhone_IsIndistinguishableFromWrongPassword(string typed)
    {
        _uow.Users.GetByPhoneAsync(Arg.Any<string>()).Returns((User?)null);
        _uow.Users.GetByEmailAsync("asim@test.ba").Returns(OrgAdmin());

        var noAccount = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _service.LoginAsync(new LoginDto(typed, "Whatever1!")));
        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _service.LoginAsync(new LoginDto("asim@test.ba", "Whatever1!")));

        Assert.Equal(noAccount.Message, wrongPassword.Message);
    }

    // A frontend cached before the identifier rename still posts `email`. Dropping that field
    // would sign those users out until their service worker updated.
    [Fact]
    public async Task Login_LegacyEmailField_StillAuthenticates()
    {
        _uow.Users.GetByEmailAsync("asim@test.ba").Returns(OrgAdmin());
        _uow.RefreshTokens.AddAsync(Arg.Any<RefreshToken>()).Returns(ci => ci.Arg<RefreshToken>());

        var result = await _service.LoginAsync(new LoginDto(null, "Correct123!", Email: "asim@test.ba"));

        Assert.Equal(3, result.Token.Split('.').Length);
    }

    // ── Refresh rotation ───────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_ValidToken_RotatesAndLinksReplacement()
    {
        var stored = new RefreshToken
        {
            Id        = 1,
            UserId    = 1,
            TokenHash = "old-hash",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };
        RefreshToken? replacement = null;
        _uow.RefreshTokens.GetByHashAsync(Arg.Any<string>()).Returns(stored);
        _uow.RefreshTokens.AddAsync(Arg.Do<RefreshToken>(t => replacement = t)).Returns(ci => ci.Arg<RefreshToken>());
        _uow.Users.GetByIdWithOrganizationAsync(1).Returns(OrgAdmin());

        var result = await _service.RefreshAsync("raw-refresh-token");

        Assert.NotNull(stored.RevokedAt);                                   // presented token is spent
        Assert.NotNull(replacement);
        Assert.Equal(replacement!.TokenHash, stored.ReplacedByTokenHash);   // rotation chain is linked
        Assert.Equal(64, result.RefreshToken.Length);
    }

    [Fact]
    public async Task Refresh_ReusedRevokedToken_RevokesWholeActiveSet()
    {
        var stored = new RefreshToken
        {
            UserId    = 1,
            TokenHash = "old-hash",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = DateTime.UtcNow.AddMinutes(-5), // already rotated → this presentation is reuse
        };
        var active1 = new RefreshToken { UserId = 1, TokenHash = "a1", ExpiresAt = DateTime.UtcNow.AddDays(7) };
        var active2 = new RefreshToken { UserId = 1, TokenHash = "a2", ExpiresAt = DateTime.UtcNow.AddDays(7) };
        _uow.RefreshTokens.GetByHashAsync(Arg.Any<string>()).Returns(stored);
        _uow.RefreshTokens.GetActiveByUserAsync(1).Returns([active1, active2]);

        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.RefreshAsync("stolen-token"));

        Assert.NotNull(active1.RevokedAt);
        Assert.NotNull(active2.RevokedAt);
        await _uow.Received().SaveChangesAsync();
    }

    [Fact]
    public async Task Refresh_ExpiredToken_Throws()
    {
        _uow.RefreshTokens.GetByHashAsync(Arg.Any<string>()).Returns(new RefreshToken
        {
            UserId    = 1,
            TokenHash = "old-hash",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
        });

        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.RefreshAsync("expired-token"));
    }

    [Fact]
    public async Task Refresh_UnknownToken_Throws()
    {
        _uow.RefreshTokens.GetByHashAsync(Arg.Any<string>()).Returns((RefreshToken?)null);

        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.RefreshAsync("garbage"));
    }

    [Fact]
    public async Task Refresh_MissingToken_Throws()
    {
        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.RefreshAsync("  "));
    }

    // ── Logout ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_ActiveToken_Revokes()
    {
        var stored = new RefreshToken { UserId = 1, TokenHash = "h", ExpiresAt = DateTime.UtcNow.AddDays(7) };
        _uow.RefreshTokens.GetByHashAsync(Arg.Any<string>()).Returns(stored);

        await _service.LogoutAsync("raw-token");

        Assert.NotNull(stored.RevokedAt);
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Logout_UnknownOrEmptyToken_IsIdempotent()
    {
        _uow.RefreshTokens.GetByHashAsync(Arg.Any<string>()).Returns((RefreshToken?)null);

        await _service.LogoutAsync("unknown");
        await _service.LogoutAsync("");

        await _uow.DidNotReceive().SaveChangesAsync();
    }
}
