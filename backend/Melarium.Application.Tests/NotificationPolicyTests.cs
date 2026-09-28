using Melarium.Application.Features.Notifications;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// The one policy table (SPEC-29): which alert runs in which phase, with which threshold and
/// priority — and from that, which channel a notification takes for a user with given settings.
/// </summary>
public class NotificationPolicyTests
{
    private readonly NotificationPolicy _policy = TestSeasons.Policy();

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    public static TheoryData<SeasonPhase> AllPhases =>
    [
        SeasonPhase.Winter, SeasonPhase.SpringBuildUp, SeasonPhase.MainSeason, SeasonPhase.LateSummer, SeasonPhase.Wintering,
    ];

    // ── The table ────────────────────────────────────────────────────────────────

    [Fact]
    public void InspectionOverdue_DoesNotRunInWinter()
    {
        Assert.False(_policy.For(NotificationType.InspectionOverdue, SeasonPhase.Winter).Enabled);
    }

    [Theory]
    [InlineData(SeasonPhase.SpringBuildUp, 30)]
    [InlineData(SeasonPhase.MainSeason, 21)]
    [InlineData(SeasonPhase.LateSummer, 30)]
    [InlineData(SeasonPhase.Wintering, 45)]
    public void InspectionOverdue_Threshold_Is21DaysInTheMainSeason_AndLongerOutsideIt(SeasonPhase phase, int days)
    {
        var rule = _policy.For(NotificationType.InspectionOverdue, phase);
        Assert.True(rule.Enabled);
        Assert.Equal(days, rule.ThresholdDays);
    }

    [Theory]
    [MemberData(nameof(AllPhases))]
    public void WorkTheBeekeeperStarted_IsNeverSilencedBySeason(SeasonPhase phase)
    {
        Assert.True(_policy.For(NotificationType.StripsLeftIn, phase).Enabled);
        Assert.True(_policy.For(NotificationType.KarencaEnded, phase).Enabled);
        Assert.True(_policy.For(NotificationType.FeedingOverdue, phase).Enabled);
        Assert.True(_policy.For(NotificationType.TreatmentRoundOverdue, phase).Enabled);
    }

    [Theory]
    [MemberData(nameof(AllPhases))]
    public void SystemNotices_AreNeverSilencedBySeason(SeasonPhase phase)
    {
        Assert.True(_policy.For(NotificationType.PlanExpiring, phase).Enabled);
        Assert.True(_policy.For(NotificationType.PlanLockPending, phase).Enabled);
        Assert.True(_policy.For(NotificationType.PasswordChanged, phase).Enabled);
    }

    [Fact]
    public void Frost_InWinter_OnlyForExtremeCold()
    {
        var rule = _policy.For(NotificationType.FrostWarning, SeasonPhase.Winter);
        Assert.True(rule.Enabled);
        Assert.Equal(-15, rule.BelowCelsius);
        Assert.Equal(NotificationPriority.Normal, rule.Priority);
    }

    [Theory]
    [InlineData(SeasonPhase.SpringBuildUp)]
    [InlineData(SeasonPhase.MainSeason)]
    public void Frost_InSpringAndMainSeason_IsCritical(SeasonPhase phase)
    {
        var rule = _policy.For(NotificationType.FrostWarning, phase);
        Assert.Equal(NotificationPriority.Critical, rule.Priority);
        Assert.Equal(0, rule.BelowCelsius);
    }

    [Fact]
    public void Frost_WhileWintering_IsTheFirstFrostOnly()
    {
        var rule = _policy.For(NotificationType.FrostWarning, SeasonPhase.Wintering);
        Assert.True(rule.OncePerPhase);
        Assert.Equal(NotificationPriority.Normal, rule.Priority);
    }

    [Theory]
    [MemberData(nameof(AllPhases))]
    public void HoneyLevelDrop_RunsOutsideWinterOnly(SeasonPhase phase)
    {
        Assert.Equal(phase != SeasonPhase.Winter, _policy.For(NotificationType.HoneyLevelDrop, phase).Enabled);
    }

    [Theory]
    [MemberData(nameof(AllPhases))]
    public void OldQueen_IsAnInfoOnceInSpring(SeasonPhase phase)
    {
        var rule = _policy.For(NotificationType.OldQueen, phase);
        Assert.Equal(phase == SeasonPhase.SpringBuildUp, rule.Enabled);
        if (rule.Enabled) Assert.Equal(NotificationPriority.Info, rule.Priority);
    }

    [Fact]
    public void KillSwitch_StillTurnsARuleOff()
    {
        var config = Substitute.For<IConfiguration>();
        config["Alerts:StaleInspection:Enabled"].Returns("false");
        var policy = TestSeasons.Policy(config);

        Assert.False(policy.For(NotificationType.InspectionOverdue, SeasonPhase.MainSeason).Enabled);
        Assert.True(policy.For(NotificationType.HoneyLevelDrop, SeasonPhase.MainSeason).Enabled);
    }

    [Fact]
    public void MainSeasonThreshold_StillReadsThePreSeasonKey()
    {
        var config = Substitute.For<IConfiguration>();
        config["Alerts:StaleInspectionDays"].Returns("14");
        var policy = TestSeasons.Policy(config);

        Assert.Equal(14, policy.For(NotificationType.InspectionOverdue, SeasonPhase.MainSeason).ThresholdDays);
    }

    // ── Weekly summary cadence ──────────────────────────────────────────────────

    [Fact]
    public void WeeklySummary_InWinter_OnlyOnTheFirstMondayOfTheMonth()
    {
        var winter = _policy.SummaryCadenceFor(SeasonPhase.Winter);
        Assert.True(winter.Monthly);
        Assert.True(winter.IsDue(D(2026, 12, 7)));   // first Monday of December
        Assert.False(winter.IsDue(D(2026, 12, 14)));
        Assert.Equal("Mjesečni pregled", winter.Title);

        var summer = _policy.SummaryCadenceFor(SeasonPhase.MainSeason);
        Assert.False(summer.Monthly);
        Assert.True(summer.IsDue(D(2026, 6, 15)));
    }

    // ── Inspection clock ────────────────────────────────────────────────────────

    [Fact]
    public void WinterDays_DoNotCount_TheClockRestartsInSpring()
    {
        var lastAutumn = D(2025, 10, 10);

        Assert.False(_policy.IsInspectionOverdue(lastAutumn, D(2026, 1, 20), 0));  // winter
        Assert.False(_policy.IsInspectionOverdue(lastAutumn, D(2026, 2, 17), 0));  // spring, day 2
        Assert.False(_policy.IsInspectionOverdue(lastAutumn, D(2026, 3, 17), 0));  // 29 days into spring
        Assert.True(_policy.IsInspectionOverdue(lastAutumn, D(2026, 3, 18), 0));   // 30
    }

    [Fact]
    public void MainSeason_Uses21Days()
    {
        var last = D(2026, 5, 1);
        Assert.False(_policy.IsInspectionOverdue(last, D(2026, 5, 21), 0));
        Assert.True(_policy.IsInspectionOverdue(last, D(2026, 5, 22), 0));
    }

    [Fact]
    public void AShorterThreshold_AppliesFromTheFirstDayOfItsPhase()
    {
        // 27 days on 16 April: not late under spring's 30, late under the main season's 21.
        var last = D(2026, 3, 20);
        Assert.False(_policy.IsInspectionOverdue(last, D(2026, 4, 15), 0));
        Assert.True(_policy.IsInspectionOverdue(last, D(2026, 4, 16), 0));
        Assert.Equal(D(2026, 4, 16), _policy.InspectionBecomesDue(last, D(2026, 4, 1), D(2026, 5, 1), 0));
    }

    [Fact]
    public void InspectionBecomesDue_IsNeverInWinter()
    {
        Assert.Null(_policy.InspectionBecomesDue(D(2025, 10, 10), D(2025, 11, 20), D(2026, 2, 15), 0));
        Assert.Equal(D(2026, 3, 18), _policy.InspectionBecomesDue(D(2025, 10, 10), D(2025, 11, 20), D(2026, 4, 1), 0));
    }

    // ── Channels ────────────────────────────────────────────────────────────────

    [Fact]
    public void Security_IsAlwaysMailed_EvenWithEmailOff()
    {
        Assert.True(_policy.ShouldEmailNow(NotificationType.PasswordChanged, NotificationPriority.Critical, EmailNotificationMode.Off));
        Assert.True(_policy.ShouldEmailNow(NotificationType.OrganizationOwnershipTransferred, NotificationPriority.Normal, EmailNotificationMode.Off));
    }

    [Fact]
    public void NormalAlerts_WaitForTheMorningEmail_WhileHumanNewsGoesAtOnce()
    {
        Assert.False(_policy.ShouldEmailNow(NotificationType.InspectionOverdue, NotificationPriority.Normal, EmailNotificationMode.All));
        Assert.False(_policy.ShouldEmailNow(NotificationType.DailyAgenda, NotificationPriority.Normal, EmailNotificationMode.All));
        Assert.True(_policy.ShouldEmailNow(NotificationType.TodoCreated, NotificationPriority.Normal, EmailNotificationMode.All));
        Assert.True(_policy.ShouldEmailNow(NotificationType.FrostWarning, NotificationPriority.Critical, EmailNotificationMode.All));
    }

    [Fact]
    public void CriticalOnly_And_Off_DoWhatTheySay()
    {
        Assert.True(_policy.ShouldEmailNow(NotificationType.FrostWarning, NotificationPriority.Critical, EmailNotificationMode.CriticalOnly));
        Assert.False(_policy.ShouldEmailNow(NotificationType.TodoCreated, NotificationPriority.Normal, EmailNotificationMode.CriticalOnly));
        Assert.False(_policy.ShouldEmailNow(NotificationType.FrostWarning, NotificationPriority.Critical, EmailNotificationMode.Off));
        Assert.False(_policy.ShouldEmailNow(NotificationType.OldQueen, NotificationPriority.Info, EmailNotificationMode.All));
    }

    [Fact]
    public void InAppSwitches_HideOnlyAlerts_AndNeverCritical()
    {
        var quiet = new NotificationSettings { NormalAlertsInApp = false, InfoAlertsInApp = false };

        Assert.False(_policy.ShowInApp(NotificationType.InspectionOverdue, NotificationPriority.Normal, quiet));
        Assert.False(_policy.ShowInApp(NotificationType.OldQueen, NotificationPriority.Info, quiet));
        Assert.True(_policy.ShowInApp(NotificationType.FrostWarning, NotificationPriority.Critical, quiet));
        Assert.True(_policy.ShowInApp(NotificationType.TodoCreated, NotificationPriority.Normal, quiet));
        Assert.True(_policy.ShowInApp(NotificationType.InspectionOverdue, NotificationPriority.Normal, null));
    }
}
