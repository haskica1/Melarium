using Melarium.Application.Common.Email;
using Melarium.Application.Features.Notifications;
using Melarium.Domain.Enums;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// How a notification looks as an e-mail (ADR-048): the button goes where the notification is about,
/// the colour follows the priority, and the footer follows the delivery policy, never the sender.
/// </summary>
public class NotificationEmailTests
{
    private static EmailContent Compose(NotificationType type, int? id = null, string? entity = null,
        NotificationPriority priority = NotificationPriority.Normal, EmailContent? custom = null, bool security = false) =>
        NotificationEmail.Compose(type, priority, "Naslov", "Poruka.", id, entity, custom, security);

    [Theory]
    [InlineData("Beehive", 7, "/beehives/7")]
    [InlineData("Apiary", 2, "/apiaries/2")]
    [InlineData("Treatment", 4, "/treatments/4")]
    [InlineData("Diet", 3, "/feedings/3")]
    [InlineData("Invitation", 9, "/invite")]
    [InlineData("SeasonPhase", 20263, "/learning?category=2")]
    [InlineData(null, null, "/")]
    public void Button_GoesWhereTheNotificationIsAbout(string? entity, int? id, string path) =>
        Assert.Equal(path, Compose(NotificationType.BeehiveAssigned, id, entity).Button!.Url);

    [Fact]
    public void OrganizationNotice_ForAPlan_OpensThePlans_NotTheOrganizationPage() =>
        // A Beekeeper cannot open /organization; every recipient of a plan notice can open /plans.
        Assert.Equal("/plans", Compose(NotificationType.PlanLockPending, 1, "Organization").Button!.Url);

    [Fact]
    public void PlainNotification_IsText_WithTheTypesIconAndCategory()
    {
        var mail = Compose(NotificationType.TodoCreated);

        Assert.Equal("Poruka.", Assert.IsType<EmailText>(Assert.Single(mail.Blocks)).Text);
        Assert.Equal("✅", mail.Icon);
        Assert.Equal("Zadatak", mail.Eyebrow);
        Assert.Equal(EmailTone.Brand, mail.Tone);
        Assert.Equal(NotificationEmail.NowReason, mail.Reason);
        Assert.True(mail.LinkSettings);
    }

    [Fact]
    public void Removal_IsGrey() =>
        Assert.Equal(EmailTone.Neutral, Compose(NotificationType.ApiaryUnassigned).Tone);

    [Fact]
    public void CriticalAlert_IsRed_AndSaysWhyItCameAtOnce()
    {
        var mail = Compose(NotificationType.FrostWarning, 2, "Apiary", NotificationPriority.Critical);

        Assert.Equal(EmailTone.Critical, mail.Tone);
        Assert.Equal("Kritično upozorenje", mail.Eyebrow);
        Assert.Equal(NotificationEmail.CriticalReason, mail.Reason);
    }

    [Fact]
    public void Security_HasNoSettingsLink_BecauseTheSettingsCannotStopIt()
    {
        var mail = Compose(NotificationType.PasswordChanged, security: true, priority: NotificationPriority.Critical);

        Assert.Equal(NotificationEmail.SecurityReason, mail.Reason);
        Assert.False(mail.LinkSettings);
    }

    [Fact]
    public void SendersContent_Wins_ExceptForTheFooter()
    {
        var custom = new EmailContent("Nagrada")
        {
            Icon = "🎁", Tone = EmailTone.Success, Reason = "Poseban razlog.",
            Button = new EmailLink("/plans", "Pogledaj paket"),
        };

        var mail = Compose(NotificationType.InvitationAccepted, 5, "Invitation", custom: custom);

        Assert.Equal("🎁", mail.Icon);
        Assert.Equal(EmailTone.Success, mail.Tone);
        Assert.Equal("/plans", mail.Button!.Url);
        Assert.Equal("Poseban razlog.", mail.Reason);
        Assert.Equal("Pozivnice", mail.Eyebrow);
        Assert.True(mail.LinkSettings);
    }
}
