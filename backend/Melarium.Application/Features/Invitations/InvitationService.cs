using System.Security.Cryptography;
using Melarium.Application.Common;
using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Localization;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Invitations.DTOs;
using Melarium.Application.Features.Notifications;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Melarium.Application.Features.Invitations;

public class InvitationService : IInvitationService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IPlanCredit _planCredit;
    private readonly INotificationService _notifications;
    private readonly IConfiguration _config;
    private readonly ILogger<InvitationService> _logger;

    public InvitationService(
        IUnitOfWork uow,
        ICurrentUser currentUser,
        IPlanCredit planCredit,
        INotificationService notifications,
        IConfiguration config,
        ILogger<InvitationService> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _planCredit = planCredit;
        _notifications = notifications;
        _config = config;
        _logger = logger;
    }

    // ── Reads ──────────────────────────────────────────────────────────────────

    public async Task<InvitationSummaryDto> GetSummaryAsync()
    {
        var userId = RequireUser();

        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new NotFoundException(nameof(User), userId);

        // Minted on first view rather than at registration: most accounts never open this page, and
        // a column that is only filled when it is needed cannot go stale for everyone else. Same
        // model as CalendarSettings.FeedToken.
        if (string.IsNullOrEmpty(user.ReferralCode))
        {
            user.ReferralCode = NewReferralCode();
            await _uow.SaveChangesAsync();
        }

        var (sent, accepted, rewardDays) = await _uow.Invitations.GetSummaryForInviterAsync(userId);

        var remaining = 0;
        if (_currentUser.OrganizationId is int orgId)
        {
            var used = await _uow.Invitations.SumRewardDaysForOrganizationAsync(orgId);
            remaining = Math.Max(0, LifetimeCapDays - used);
        }

        return new InvitationSummaryDto
        {
            SentCount = sent,
            AcceptedCount = accepted,
            RewardDaysEarned = rewardDays,
            RewardDaysRemaining = remaining,
            ShareUrl = FrontendUrl.Build(_config, $"/register?ref={user.ReferralCode}"),
            InviteeTrialDays = InviteeTrialDays,
            RewardDaysPerInvite = RewardDaysPerInvite,
        };
    }

    public async Task<IEnumerable<InvitationDto>> GetMineAsync()
    {
        var invitations = await _uow.Invitations.GetByInviterAsync(RequireUser());
        return invitations.Select(Map);
    }

    public async Task<ReferralInviterDto?> GetInviterByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        var inviter = await _uow.Users.GetByReferralCodeAsync(code.Trim());
        return inviter is null
            ? null
            : new ReferralInviterDto(inviter.FirstName, InviteeTrialDays);
    }

    // ── Seams called from AuthService ──────────────────────────────────────────

    public async Task<int> ResolveTrialDaysAsync(string? referralCode, string email)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(referralCode)
                && await _uow.Users.GetByReferralCodeAsync(referralCode.Trim()) is not null)
                return InviteeTrialDays;

            // Someone who was e-mailed an invitation but typed the address by hand instead of
            // following the link is just as invited — they must not silently get the shorter trial.
            var pending = await _uow.Invitations.GetLatestPendingByCanonicalEmailAsync(Canonicalize(email));
            return pending is not null ? InviteeTrialDays : StandardTrialDays;
        }
        catch (Exception ex)
        {
            // This runs before the organization exists; a failure here would fail the sign-up.
            _logger.LogError(ex, "Referral trial lookup failed — falling back to the standard trial");
            return StandardTrialDays;
        }
    }

    public async Task TryAttributeRegistrationAsync(int newUserId, int newOrganizationId, string email, string? referralCode)
    {
        try
        {
            var canonical = Canonicalize(email);
            var now = DateTime.UtcNow;

            var inviter = string.IsNullOrWhiteSpace(referralCode)
                ? null
                : await _uow.Users.GetByReferralCodeAsync(referralCode.Trim());

            var invitation = await _uow.Invitations.GetLatestPendingByCanonicalEmailAsync(canonical);

            // The link wins over the address: following someone's link is an explicit act, while a
            // matching address is an inference. When both point at the same person it makes no
            // difference; when they disagree, the click is the better evidence.
            if (invitation is not null && inviter is not null && invitation.InviterUserId != inviter.Id)
                invitation = null;

            if (invitation is null)
            {
                if (inviter is null) return;   // no code, no matching invitation — nothing to credit

                invitation = new Invitation
                {
                    Source = InvitationSource.ShareLink,
                    InviterUserId = inviter.Id,
                    InviterOrganizationId = inviter.OrganizationId,
                    Email = email.Trim().ToLower(),
                    EmailCanonical = canonical,
                    CreatedAt = now,
                };
                await _uow.Invitations.AddAsync(invitation);
            }

            invitation.Status = InvitationStatus.Accepted;
            invitation.AcceptedAt = now;
            invitation.AcceptedByUserId = newUserId;
            invitation.AcceptedOrganizationId = newOrganizationId;

            await _uow.SaveChangesAsync();

            // Only ever one invitation is credited per registration. Other pending rows to the same
            // address stay "Sent" on purpose: two people cannot both be paid for one sign-up, and
            // that is precisely the double-payout the caps exist to prevent.
            if (invitation.InviterUserId is int inviterId)
            {
                var invitee = await _uow.Users.GetByIdAsync(newUserId);
                await _notifications.NotifyAsync(
                    inviterId,
                    "Vaša pozivnica je prihvaćena",
                    // First name only — if they registered with a different address than the one
                    // invited, echoing it back would leak their private address to the inviter.
                    $"{invitee?.FirstName ?? "Novi korisnik"} se pridružio Melariumu preko vaše pozivnice.",
                    NotificationType.InvitationAccepted,
                    invitation.Id, nameof(Invitation));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Referral attribution failed for user {UserId} — registration is unaffected", newUserId);
        }
    }

    public async Task TryGrantRewardForVerifiedUserAsync(int userId)
    {
        try
        {
            var invitation = await _uow.Invitations.GetUnrewardedByAcceptedUserAsync(userId);
            if (invitation is null) return;

            var days = await ResolveGrantableDaysAsync(invitation);

            var result = days > 0 && invitation.InviterOrganizationId is int orgId
                ? await _planCredit.GrantDaysAsync(orgId, days)
                : new PlanCreditResult(0, PlanType.Free, WasUpgrade: false);

            // Settled either way — including when nothing was granted — so a capped or lifetime-plan
            // organization is not re-examined on every future verification.
            invitation.RewardDays = result.DaysGranted;
            invitation.RewardGrantedAt = DateTime.UtcNow;

            await _uow.SaveChangesAsync();

            if (result.DaysGranted > 0 && invitation.InviterUserId is int inviterId)
            {
                var invitee = await _uow.Users.GetByIdAsync(userId);
                await _notifications.NotifyAsync(
                    inviterId,
                    "Nagrada za pozivnicu",
                    // Worded from what actually happened: "Pro" only when the organization was
                    // genuinely raised to Pro. A Max or Partner customer being told they received
                    // "Pro" would read as a downgrade.
                    result.WasUpgrade
                        ? $"{invitee?.FirstName ?? "Pozvana osoba"} je potvrdio e-poštu — dobili ste {result.DaysGranted} dana Pro paketa."
                        : $"{invitee?.FirstName ?? "Pozvana osoba"} je potvrdio e-poštu — vaš paket je produžen za {result.DaysGranted} dana.",
                    NotificationType.InvitationAccepted,
                    invitation.Id, nameof(Invitation));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Referral reward failed for verified user {UserId} — verification is unaffected", userId);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// How many days this invitation may actually pay, after every guard. Returns 0 rather than
    /// throwing: "no reward" is a normal outcome here, not an error.
    /// </summary>
    private async Task<int> ResolveGrantableDaysAsync(Invitation invitation)
    {
        if (invitation.InviterOrganizationId is not int orgId) return 0;

        // The same person invited from two of your own accounts pays out once.
        if (invitation.AcceptedOrganizationId is int acceptedOrgId
            && await _uow.Invitations.AnyRewardForAcceptedOrganizationAsync(acceptedOrgId))
            return 0;

        var used = await _uow.Invitations.SumRewardDaysForOrganizationAsync(orgId);
        var remaining = LifetimeCapDays - used;
        if (remaining <= 0) return 0;

        // The rolling window bounds how fast an attack can run, which is what makes it noticeable.
        var since = DateTime.UtcNow.AddDays(-RollingWindowDays);
        var recent = await _uow.Invitations.CountRewardsForOrganizationSinceAsync(orgId, since);
        if (recent >= MaxRewardsPerWindow) return 0;

        // Clamped, not refused: a lifetime cap is a total, so the last invitation pays the remainder.
        return Math.Min(RewardDaysPerInvite, remaining);
    }

    private static InvitationDto Map(Invitation i) => new(
        i.Id,
        i.Source,
        BsLabels.Label(i.Source),
        i.Email,
        i.Status,
        StatusLabel(i),
        i.RewardDays,
        i.AcceptedAt,
        i.CreatedAt);

    /// <summary>
    /// Three labels from two stored states. "Accepted but not yet rewarded" is its own thing to a
    /// user: they did join, and the days are waiting on the invitee confirming their address.
    /// Showing a bare "Pridružio se" there would look like the reward was quietly withheld.
    /// </summary>
    private static string StatusLabel(Invitation i) => i.Status switch
    {
        InvitationStatus.Accepted when i.RewardGrantedAt is null => "Registrovao se — čeka potvrdu",
        InvitationStatus.Accepted => "Pridružio se",
        _ => "Poslano",
    };

    /// <summary>
    /// Comparison key only. The "+tag" trick is the cheapest way around both the cooldown and the
    /// reward cap, so it is stripped — but dots are left alone: they are significant at most
    /// providers, and removing them would treat two different people as one.
    /// </summary>
    private static string Canonicalize(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var at = normalized.LastIndexOf('@');
        if (at <= 0) return normalized;

        var local = normalized[..at];
        var domain = normalized[at..];

        var plus = local.IndexOf('+');
        if (plus >= 0) local = local[..plus];

        return local + domain;
    }

    /// <summary>128-bit. Long enough that the code cannot be walked to count the user base.</summary>
    private static string NewReferralCode() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    private int RewardDaysPerInvite => Setting("Invitations:Reward:DaysPerAccepted", 30);
    private int LifetimeCapDays => Setting("Invitations:Reward:MaxDaysPerOrganization", 180);
    private int MaxRewardsPerWindow => Setting("Invitations:Reward:MaxPerRolling30Days", 5);
    private int RollingWindowDays => Setting("Invitations:Reward:RollingWindowDays", 30);
    private int InviteeTrialDays => Setting("Invitations:InviteeTrialDays", 60);
    private int StandardTrialDays => Setting("Plans:Trial:Days", 30);

    private int Setting(string key, int fallback) =>
        int.TryParse(_config[key], out var value) && value > 0 ? value : fallback;

    private int RequireUser() =>
        _currentUser.UserId ?? throw new ForbiddenAccessException("Morate biti prijavljeni.");
}
