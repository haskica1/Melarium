namespace Melarium.Application.Features.Invitations.DTOs;

/// <summary>The stats strip and the share card on /invite (SPEC-15).</summary>
public class InvitationSummaryDto
{
    /// <summary>Invitations actually e-mailed. Share-link sign-ups are not "sent" by anyone.</summary>
    public int SentCount { get; set; }

    public int AcceptedCount { get; set; }

    public int RewardDaysEarned { get; set; }

    /// <summary>Days still available under the per-organization lifetime cap; 0 once it is reached.</summary>
    public int RewardDaysRemaining { get; set; }

    /// <summary>
    /// The full share URL, built server-side: the client cannot know <c>FrontendUrl</c>, which
    /// differs from the browser origin under the dev proxy.
    /// </summary>
    public string ShareUrl { get; set; } = string.Empty;

    /// <summary>Days the invited person receives — shown so the inviter knows what they are offering.</summary>
    public int InviteeTrialDays { get; set; }

    /// <summary>Days the inviter earns per accepted-and-verified invitation.</summary>
    public int RewardDaysPerInvite { get; set; }
}
