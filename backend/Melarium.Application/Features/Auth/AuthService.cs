using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Models;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Auth.DTOs;
using Melarium.Application.Features.Notifications;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Melarium.Application.Features.Auth;

public class AuthService : IAuthService
{
    /// <summary>
    /// A BCrypt hash of a throwaway value, verified against when the email is unknown so that a
    /// failed login costs the same whether or not the account exists (no user enumeration by timing).
    /// </summary>
    private static readonly string DummyPasswordHash =
        BCrypt.Net.BCrypt.HashPassword("melarium-timing-equalizer");

    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _config;
    private readonly INotificationService _notifications;
    private readonly IEmailQueue _emailQueue;
    private readonly ISessionRevoker _sessions;

    public AuthService(
        IUnitOfWork uow,
        IConfiguration config,
        INotificationService notifications,
        IEmailQueue emailQueue,
        ISessionRevoker sessions)
    {
        _uow = uow;
        _config = config;
        _notifications = notifications;
        _emailQueue = emailQueue;
        _sessions = sessions;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _uow.Users.GetByEmailAsync(dto.Email.Trim().ToLower());

        // Always run a verify — skipping it for unknown emails makes "no such user" measurably
        // faster than "wrong password", which is enough to enumerate accounts.
        var passwordMatches = BCrypt.Net.BCrypt.Verify(dto.Password, user?.PasswordHash ?? DummyPasswordHash);

        if (user is null || !passwordMatches)
            throw new UnauthorizedException("Pogrešna e-pošta ili lozinka.");

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

        // Confirm the address is real. Delivery is best-effort and sign-in is not blocked on it —
        // a failed email must never leave someone unable to use the account they just created.
        await SendVerificationEmailAsync(user);

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
            await _sessions.RevokeAllAsync(stored.UserId);
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

    // ── Password reset ─────────────────────────────────────────────────────────

    public async Task ForgotPasswordAsync(string email)
    {
        var user = await _uow.Users.GetByEmailAsync(email.Trim().ToLower());

        // No account? Do nothing, but report success to the caller. Telling them "unknown address"
        // would turn this endpoint into a free account-enumeration oracle.
        if (user is null) return;

        var raw = await IssueUserTokenAsync(
            user.Id, UserTokenPurpose.PasswordReset, GetHours("Auth:PasswordResetTokenHours", 2));

        _emailQueue.Enqueue(new QueuedEmail(
            user.Id,
            "Zahtjev za promjenu lozinke",
            "Primili smo zahtjev za promjenu lozinke na vašem računu. Link vrijedi ograničeno vrijeme i "
            + "može se iskoristiti samo jednom. Ako niste vi tražili promjenu, slobodno zanemarite ovu poruku — "
            + "vaša lozinka ostaje nepromijenjena.",
            BuildFrontendUrl($"/reset-password?token={raw}"),
            "Postavi novu lozinku"));
    }

    public async Task ResetPasswordAsync(ResetPasswordDto dto)
    {
        var token = await _uow.UserTokens.GetByHashAsync(Hash(dto.Token), UserTokenPurpose.PasswordReset);

        if (token is null || token.UsedAt is not null || token.ExpiresAt <= DateTime.UtcNow)
            throw new BusinessRuleException(
                "Link za promjenu lozinke nije važeći ili je istekao. Zatražite novi.");

        var user = await _uow.Users.GetByIdAsync(token.UserId)
            ?? throw new BusinessRuleException("Link za promjenu lozinke nije važeći ili je istekao. Zatražite novi.");

        var now = DateTime.UtcNow;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        token.UsedAt = now;

        // Receiving this email proved control of the mailbox — no separate confirmation needed.
        user.EmailVerifiedAt ??= now;

        // Whoever triggered the reset (often: the account was compromised) must not keep a
        // working session. Everything signed in with the old password is cut off.
        await _sessions.RevokeAllAsync(user.Id);

        await _uow.SaveChangesAsync();

        await _notifications.NotifyAsync(
            user.Id,
            "Lozinka je promijenjena",
            "Vaša lozinka je uspješno promijenjena i odjavljeni ste sa svih uređaja. "
            + "Ako to niste bili vi, odmah zatražite novu promjenu lozinke i kontaktirajte podršku.",
            NotificationType.PasswordChanged);
    }

    // ── Email verification ─────────────────────────────────────────────────────

    public async Task VerifyEmailAsync(string token)
    {
        var stored = await _uow.UserTokens.GetByHashAsync(Hash(token), UserTokenPurpose.EmailVerification)
            ?? throw new BusinessRuleException("Link za potvrdu e-pošte nije važeći.");

        var user = await _uow.Users.GetByIdAsync(stored.UserId)
            ?? throw new BusinessRuleException("Link za potvrdu e-pošte nije važeći.");

        // Clicking the link twice (or a mail client pre-fetching it) must not look like a failure.
        if (user.EmailVerifiedAt is not null) return;

        if (stored.UsedAt is not null || stored.ExpiresAt <= DateTime.UtcNow)
            throw new BusinessRuleException("Link za potvrdu e-pošte je istekao. Zatražite novi.");

        var now = DateTime.UtcNow;
        user.EmailVerifiedAt = now;
        stored.UsedAt = now;

        await _uow.SaveChangesAsync();
    }

    public async Task ResendVerificationEmailAsync(int userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new NotFoundException(nameof(User), userId);

        if (user.EmailVerifiedAt is not null) return;

        await SendVerificationEmailAsync(user);
        await _uow.SaveChangesAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Issues a verification token and queues the email. Does not save — the caller decides the
    /// transaction boundary (registration saves as part of creating the account).
    /// </summary>
    private async Task SendVerificationEmailAsync(User user)
    {
        var raw = await IssueUserTokenAsync(
            user.Id, UserTokenPurpose.EmailVerification, GetHours("Auth:EmailVerificationTokenHours", 48));

        _emailQueue.Enqueue(new QueuedEmail(
            user.Id,
            "Potvrdite vašu e-poštu",
            "Da biste primali obavijesti i mogli vratiti pristup računu ako zaboravite lozinku, "
            + "potvrdite da je ova adresa vaša.",
            BuildFrontendUrl($"/verify-email?token={raw}"),
            "Potvrdi e-poštu"));
    }

    /// <summary>
    /// Invalidates the user's outstanding tokens of this purpose and issues a fresh one.
    /// Returns the raw value — only its hash is stored, so this is the one chance to send it.
    /// </summary>
    private async Task<string> IssueUserTokenAsync(int userId, UserTokenPurpose purpose, int lifetimeHours)
    {
        // A newly requested link supersedes earlier ones, so an old email can't be replayed.
        var outstanding = await _uow.UserTokens.GetActiveByUserAsync(userId, purpose);
        var now = DateTime.UtcNow;
        foreach (var old in outstanding)
            old.UsedAt = now;

        var raw = GenerateRawToken();
        await _uow.UserTokens.AddAsync(new UserToken
        {
            UserId    = userId,
            Purpose   = purpose,
            TokenHash = Hash(raw),
            ExpiresAt = now.AddHours(lifetimeHours),
        });
        await _uow.SaveChangesAsync();

        return raw;
    }

    private string BuildFrontendUrl(string pathAndQuery)
    {
        var baseUrl = _config["FrontendUrl"] ?? _config["App:PublicBaseUrl"] ?? "http://localhost:5173";
        return $"{baseUrl.TrimEnd('/')}{pathAndQuery}";
    }

    private int GetHours(string key, int fallback) =>
        int.TryParse(_config[key], out var value) && value > 0 ? value : fallback;


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
