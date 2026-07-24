using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Features.Auth.DTOs;
using Melarium.Application.Features.Notifications;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Melarium.Application.Features.Auth;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _config;
    private readonly INotificationService _notifications;

    public AuthService(IUnitOfWork uow, IConfiguration config, INotificationService notifications)
    {
        _uow = uow;
        _config = config;
        _notifications = notifications;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _uow.Users.GetByEmailAsync(dto.Email.Trim().ToLower())
            ?? throw new BusinessRuleException("Invalid email or password.");

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            throw new BusinessRuleException("Invalid email or password.");

        return await IssueTokensAsync(user);
    }

    public async Task<LoginResponseDto> RegisterAsync(RegisterDto dto)
    {
        var email = dto.Email.Trim().ToLower();

        var existing = await _uow.Users.GetByEmailAsync(email);
        if (existing != null)
            throw new BusinessRuleException($"A user with email '{dto.Email}' already exists.");

        var now = DateTime.UtcNow;

        // The registrant becomes the Organization Admin of a brand-new organisation.
        // OrganizationAdmin requires an organisation and must NOT have an apiary
        // (mirrors AdminService's role/org/apiary consistency rules).
        // New organisations start on a Pro trial (SPEC-09) — just a pre-set expiring Pro:
        // the computed effective plan falls back to Free after PlanValidUntil, no extra machinery.
        var trialDays = int.TryParse(_config["Plans:Trial:Days"], out var d) ? d : 30;
        var organization = new Organization
        {
            Name = dto.OrganizationName.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.OrganizationDescription)
                ? null
                : dto.OrganizationDescription.Trim(),
            Plan = PlanType.Pro,
            PlanValidUntil = now.Date.AddDays(trialDays),
            PlanNotes = "Probni period",
            CreatedAt = now,
        };

        var user = new User
        {
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = UserRole.OrganizationAdmin,
            Organization = organization,
            CreatedAt = now,
        };

        // Insert the organisation (creator still null) and the user together. We must NOT set
        // Organization.CreatedBy here: org → CreatedById → user → OrganizationId → org is a cycle
        // EF can't order within a single INSERT batch (it throws "circular dependency detected").
        // Adding the user pulls in the org via its navigation, so both rows are created atomically.
        await _uow.Users.AddAsync(user);
        await _uow.SaveChangesAsync();

        // Now that the user has an id, stamp it as the organisation's creator — a plain UPDATE on
        // the still-tracked org. CreatedById is nullable, so even if this save failed the org + user
        // would remain valid.
        organization.CreatedById = user.Id;
        await _uow.SaveChangesAsync();

        // Welcome notification, mirroring admin-created accounts.
        await _notifications.NotifyAsync(
            user.Id,
            "Dobrodošli u Melarium!",
            $"Vaša organizacija '{organization.Name}' je spremna. Vi ste njen administrator — počnite dodavanjem prvog pčelinjaka.",
            NotificationType.AccountCreated);

        return await IssueTokensAsync(user);
    }

    public async Task<LoginResponseDto> RefreshAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new UnauthorizedException("Refresh token is required.");

        var stored = await _uow.RefreshTokens.GetByHashAsync(Hash(refreshToken))
            ?? throw new UnauthorizedException("Invalid refresh token.");

        // Reuse of an already-rotated/revoked token signals theft — revoke the user's whole active set.
        if (stored.RevokedAt is not null)
        {
            await RevokeAllActiveForUserAsync(stored.UserId);
            await _uow.SaveChangesAsync();
            throw new UnauthorizedException("Refresh token has been revoked.");
        }

        if (stored.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedException("Refresh token has expired.");

        var user = await _uow.Users.GetByIdWithOrganizationAsync(stored.UserId)
            ?? throw new UnauthorizedException("User no longer exists.");

        // Rotate: revoke the presented token and link it to the replacement issued below.
        return await IssueTokensAsync(user, newRefreshHash =>
        {
            stored.RevokedAt = DateTime.UtcNow;
            stored.ReplacedByTokenHash = newRefreshHash;
        });
    }

    public async Task LogoutAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;

        var stored = await _uow.RefreshTokens.GetByHashAsync(Hash(refreshToken));
        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = DateTime.UtcNow;
            await _uow.SaveChangesAsync();
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Issues an access token + a new (persisted) refresh token for the user. <paramref name="onNewRefreshHash"/>
    /// runs just before persistence so a rotation caller can revoke the old token in the same transaction.
    /// </summary>
    private async Task<LoginResponseDto> IssueTokensAsync(User user, Action<string>? onNewRefreshHash = null)
    {
        var (accessToken, accessExpiresAt) = GenerateAccessToken(user);

        var rawRefresh = GenerateRawToken();
        var refreshHash = Hash(rawRefresh);
        onNewRefreshHash?.Invoke(refreshHash);

        var refreshDays = int.TryParse(_config["Jwt:RefreshTokenDays"], out var d) ? d : 14;
        await _uow.RefreshTokens.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshDays),
        });
        await _uow.SaveChangesAsync();

        IReadOnlyList<int> assignedBeehiveIds = user.Role == UserRole.Beekeeper
            ? [.. await _uow.Users.GetAssignedBeehiveIdsAsync(user.Id)]
            : [];

        return new LoginResponseDto(
            Token: accessToken,
            RefreshToken: rawRefresh,
            AccessTokenExpiresAt: accessExpiresAt,
            Email: user.Email,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Role: user.Role.ToString(),
            OrganizationId: user.OrganizationId,
            OrganizationName: user.Organization?.Name,
            AssignedBeehiveIds: assignedBeehiveIds
        );
    }

    private async Task RevokeAllActiveForUserAsync(int userId)
    {
        var active = await _uow.RefreshTokens.GetActiveByUserAsync(userId);
        var now = DateTime.UtcNow;
        foreach (var token in active)
            token.RevokedAt = now;
    }

    private (string Token, DateTime ExpiresAt) GenerateAccessToken(User user)
    {
        var secret = _config["Jwt:Secret"]!;
        var issuer = _config["Jwt:Issuer"]!;
        var audience = _config["Jwt:Audience"]!;
        var minutes = int.TryParse(_config["Jwt:AccessTokenMinutes"] ?? _config["Jwt:ExpiryMinutes"], out var m) ? m : 30;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(minutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (user.OrganizationId.HasValue)
            claims.Add(new Claim("organizationId", user.OrganizationId.Value.ToString()));

        if (user.ApiaryId.HasValue)
            claims.Add(new Claim("apiaryId", user.ApiaryId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    /// <summary>A 256-bit cryptographically-random token, hex-encoded.</summary>
    private static string GenerateRawToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    /// <summary>SHA-256 of the raw token (hex). Only this is persisted.</summary>
    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
