using Melarium.Application.Common.Seasons;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Features.Notifications;

/// <summary>
/// See <see cref="INotificationPolicy"/>. <see cref="Seasonal"/> is the table; everything else reads
/// from it. Thresholds and the extreme-cold limit come from <c>Alerts:Seasons</c> with the SPEC-29
/// defaults.
/// </summary>
public sealed class NotificationPolicy : INotificationPolicy
{
    private static readonly TimeSpan Day = TimeSpan.FromDays(1);

    // Rules tied to work the beekeeper started — strips in, karenca, a feeding or treatment protocol —
    // are never silenced by the season: nobody else is going to remind them.
    private static readonly HashSet<NotificationType> SwitchableAlerts =
    [
        NotificationType.InspectionOverdue, NotificationType.HoneyLevelDrop, NotificationType.FrostWarning,
        NotificationType.OldQueen, NotificationType.StripsLeftIn, NotificationType.KarencaEnded,
        NotificationType.FeedingOverdue, NotificationType.TreatmentRoundOverdue, NotificationType.SeasonPhaseStarted,
    ];

    private static readonly NotificationType[] DigestTypes =
    [
        .. SwitchableAlerts, NotificationType.PlanExpiring, NotificationType.WeeklySummary,
    ];

    private readonly ISeasonCalendar _seasons;
    private readonly IConfiguration _config;
    private readonly Dictionary<SeasonPhase, int> _inspectionDays;
    private readonly double _extremeColdC;

    public NotificationPolicy(ISeasonCalendar seasons, IConfiguration config)
    {
        _seasons = seasons;
        _config = config;

        _inspectionDays = new()
        {
            [SeasonPhase.SpringBuildUp] = GetInt("Alerts:Seasons:InspectionOverdueDays:SpringBuildUp", 30),
            // Falls back to the pre-SPEC-29 key, which meant exactly this before seasons existed.
            [SeasonPhase.MainSeason]    = GetInt("Alerts:Seasons:InspectionOverdueDays:MainSeason",
                                                 GetInt("Alerts:StaleInspectionDays", 21)),
            [SeasonPhase.LateSummer]    = GetInt("Alerts:Seasons:InspectionOverdueDays:LateSummer", 30),
            [SeasonPhase.Wintering]     = GetInt("Alerts:Seasons:InspectionOverdueDays:Wintering", 45),
        };
        _extremeColdC = double.TryParse(config["Alerts:Seasons:ExtremeColdC"],
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var c) ? c : -15;
        PhaseNoticeDays = GetInt("Alerts:Seasons:PhaseNoticeDays", 14);
    }

    public int PhaseNoticeDays { get; }

    public IReadOnlyCollection<NotificationType> MorningDigestTypes => DigestTypes;

    public AlertRule For(NotificationType type, SeasonPhase phase)
    {
        var rule = Seasonal(type, phase);
        return rule.Enabled && !SwitchedOn(type) ? rule with { Enabled = false } : rule;
    }

    /// <summary>The table itself — the season's say, before the operator's kill switch.</summary>
    private AlertRule Seasonal(NotificationType type, SeasonPhase phase) => (type, phase) switch
    {
        // No hive is opened in winter, so there is nothing to be late with. Out of the main season the
        // reminder comes later and repeats every two weeks instead of every week.
        (NotificationType.InspectionOverdue, SeasonPhase.Winter) => AlertRule.Off,
        (NotificationType.InspectionOverdue, SeasonPhase.MainSeason) =>
            new(true, NotificationPriority.Normal, 7 * Day, ThresholdDays: _inspectionDays[phase]),
        (NotificationType.InspectionOverdue, _) =>
            new(true, NotificationPriority.Normal, 14 * Day, ThresholdDays: _inspectionDays[phase]),

        (NotificationType.HoneyLevelDrop, SeasonPhase.Winter) => AlertRule.Off,
        (NotificationType.HoneyLevelDrop, _) => new(true, NotificationPriority.Normal, 7 * Day),

        // Frost in winter is the weather, not news — only extreme cold is worth a message. A spring
        // frost can kill brood and blossom, so it is the one alert that is Critical. Going into winter,
        // the first frost is the signal to finish the autumn work; the tenth is not.
        (NotificationType.FrostWarning, SeasonPhase.Winter) =>
            new(true, NotificationPriority.Normal, 7 * Day, BelowCelsius: _extremeColdC),
        (NotificationType.FrostWarning, SeasonPhase.SpringBuildUp or SeasonPhase.MainSeason) =>
            new(true, NotificationPriority.Critical, 3 * Day, BelowCelsius: 0),
        (NotificationType.FrostWarning, SeasonPhase.Wintering) =>
            new(true, NotificationPriority.Normal, TimeSpan.Zero, OncePerPhase: true, BelowCelsius: 0),
        (NotificationType.FrostWarning, _) =>
            new(true, NotificationPriority.Normal, 3 * Day, BelowCelsius: 0),

        // Replacing a queen is planned in spring; once a year is enough.
        (NotificationType.OldQueen, SeasonPhase.SpringBuildUp) => new(true, NotificationPriority.Info, 300 * Day),
        (NotificationType.OldQueen, _) => AlertRule.Off,

        (NotificationType.StripsLeftIn, _)          => new(true, NotificationPriority.Normal, 7 * Day),
        (NotificationType.KarencaEnded, _)          => new(true, NotificationPriority.Normal, 7 * Day),
        (NotificationType.FeedingOverdue, _)        => new(true, NotificationPriority.Normal, 3 * Day),
        (NotificationType.TreatmentRoundOverdue, _) => new(true, NotificationPriority.Normal, 3 * Day),

        (NotificationType.PlanExpiring, _)    => new(true, NotificationPriority.Normal, 7 * Day),
        // Wider than the notice window, so the two-day run-up produces one message, not two.
        (NotificationType.PlanLockPending, _) =>
            new(true, NotificationPriority.Critical, (GetInt("Alerts:PlanLockNoticeDays", 2) + 1) * Day),

        (NotificationType.SeasonPhaseStarted, _) =>
            new(true, NotificationPriority.Normal, TimeSpan.Zero, OncePerPhase: true),

        // The weekly summary's rhythm lives in SummaryCadenceFor, not in a dedup window.
        (NotificationType.WeeklySummary, _) => new(true, NotificationPriority.Normal, TimeSpan.Zero),

        _ => new(true, PriorityOf(type), TimeSpan.Zero),
    };

    public NotificationPriority PriorityOf(NotificationType type) => type switch
    {
        NotificationType.PlanLockPending or NotificationType.PasswordChanged => NotificationPriority.Critical,
        NotificationType.OldQueen
            or NotificationType.LearningTopicPublished
            or NotificationType.LearningTopicSubmitted
            or NotificationType.FeedbackSubmitted => NotificationPriority.Info,
        _ => NotificationPriority.Normal,
    };

    public bool IsSecurity(NotificationType type) => type is
        NotificationType.PasswordChanged or
        NotificationType.AccountCreated or
        NotificationType.OrganizationOwnershipTransferred;

    public bool IsSwitchableAlert(NotificationType type) => SwitchableAlerts.Contains(type);

    public bool ShouldEmailNow(NotificationType type, NotificationPriority priority, EmailNotificationMode mode)
    {
        if (IsSecurity(type)) return true;
        if (priority == NotificationPriority.Info) return false;

        return mode switch
        {
            EmailNotificationMode.Off          => false,
            EmailNotificationMode.CriticalOnly => priority == NotificationPriority.Critical,
            // Normal scan output and the agenda wait for the morning e-mail; what a person just did
            // (a todo for you, an assignment) still arrives at once, as before.
            _ => priority == NotificationPriority.Critical
                 || !(DigestTypes.Contains(type) || type == NotificationType.DailyAgenda),
        };
    }

    public bool ShowInApp(NotificationType type, NotificationPriority priority, NotificationSettings? settings)
    {
        if (settings is null || priority == NotificationPriority.Critical || !IsSwitchableAlert(type)) return true;
        return priority == NotificationPriority.Info ? settings.InfoAlertsInApp : settings.NormalAlertsInApp;
    }

    public bool IsInspectionOverdue(DateOnly lastActivity, DateOnly date, int shiftDays)
    {
        var season = _seasons.For(date, shiftDays);
        if (Seasonal(NotificationType.InspectionOverdue, season.Phase) is not { Enabled: true, ThresholdDays: int threshold })
            return false;

        // Not in winter here, so this cycle's spring has already started. A last inspection before it
        // means a winter came in between, and the clock starts again with the spring.
        var springStart = _seasons.CycleFor(date, shiftDays)[1].Start;
        var anchor = lastActivity < springStart ? springStart : lastActivity;

        return date.DayNumber - anchor.DayNumber >= threshold;
    }

    public DateOnly? InspectionBecomesDue(DateOnly lastActivity, DateOnly from, DateOnly to, int shiftDays)
    {
        var wasOverdue = IsInspectionOverdue(lastActivity, from.AddDays(-1), shiftDays);
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var overdue = IsInspectionOverdue(lastActivity, day, shiftDays);
            if (overdue && !wasOverdue) return day;
            wasOverdue = overdue;
        }
        return null;
    }

    public SummaryCadence SummaryCadenceFor(SeasonPhase phase) =>
        phase == SeasonPhase.Winter ? SummaryCadence.FirstMondayOfMonth : SummaryCadence.Weekly;

    // ── Config ──────────────────────────────────────────────────────────────────

    private bool SwitchedOn(NotificationType type)
    {
        // InspectionOverdue predates its own name in the config: the switch was always StaleInspection.
        var key = type == NotificationType.InspectionOverdue ? "StaleInspection" : type.ToString();
        return !bool.TryParse(_config[$"Alerts:{key}:Enabled"], out var enabled) || enabled;
    }

    private int GetInt(string key, int fallback) => int.TryParse(_config[key], out var v) ? v : fallback;
}
