using Melarium.Application.Common.Email;
using Melarium.Application.Common.Localization;

namespace Melarium.Application.Features.Invitations;

/// <summary>The two moments of an invitation, as e-mails (ADR-048).</summary>
public static class InvitationEmails
{
    public static EmailContent Joined(string text) => new("Vaša pozivnica je prihvaćena")
    {
        Tone = EmailTone.Success,
        Blocks = [new EmailText(text)],
        Button = new EmailLink("/invite", "Pozovi još nekoga"),
    };

    /// <summary>The days as one big number; "Pro" only when the organization really went up to Pro.</summary>
    public static EmailContent Reward(string text, int days, bool wasUpgrade)
    {
        var granted = BsLabels.Count(days, "dan", "dana", "dana");
        var unit = BsLabels.Word(days, "dan", "dana", "dana");

        return new EmailContent("Nagrada za pozivnicu")
        {
            Subject = wasUpgrade ? $"Dobili ste {granted} Pro paketa 🎁" : $"Paket je produžen za {granted} 🎁",
            Icon = "🎁",
            Tone = EmailTone.Success,
            Blocks =
            [
                new EmailStats([new EmailStat($"+{days}", wasUpgrade ? $"{unit} Pro paketa" : $"{unit} na vašem paketu", EmailTone.Success)]),
                new EmailText(text),
            ],
            Button = new EmailLink("/plans", "Pogledaj paket"),
        };
    }
}
