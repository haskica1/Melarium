using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Notifications;

/// <summary>
/// The single notification policy (SPEC-29): for every <see cref="NotificationType"/> and season phase,
/// whether the rule runs, its threshold, its dedup window and its priority — and, from that, which
/// channel a notification takes for a given user. The alert scan, the weekly summary, the daily agenda,
/// the ICS feed and the dashboard all ask this, so the agenda cannot recommend an inspection in the
/// same winter the alerts are silent about.
/// </summary>
public interface INotificationPolicy
{
    /// <summary>The table row for one alert type in one phase, including the <c>Alerts:{Rule}:Enabled</c> switch.</summary>
    AlertRule For(NotificationType type, SeasonPhase phase);

    /// <summary>Priority of a type that is not season-dependent — event-driven and system notifications.</summary>
    NotificationPriority PriorityOf(NotificationType type);

    /// <summary>Always mailed, whatever the user chose (password changed, new account, organization handed over).</summary>
    bool IsSecurity(NotificationType type);

    /// <summary>A scan alert the user may hide in the app with the Normal / Info switches.</summary>
    bool IsSwitchableAlert(NotificationType type);

    /// <summary>Scan output whose Normal rows are collected into the one morning e-mail (the agenda is composed separately).</summary>
    IReadOnlyCollection<NotificationType> MorningDigestTypes { get; }

    /// <summary>Whether a notification goes out by e-mail at the moment it is created.</summary>
    bool ShouldEmailNow(NotificationType type, NotificationPriority priority, EmailNotificationMode mode);

    /// <summary>Whether a notification is created in the app at all for a user with these settings.</summary>
    bool ShowInApp(NotificationType type, NotificationPriority priority, NotificationSettings? settings);

    /// <summary>
    /// Whether a hive last inspected (or created) on <paramref name="lastActivity"/> is overdue on
    /// <paramref name="date"/>. Never in winter, and winter days never count: the clock restarts on
    /// the first day of spring, or every hive would be "120 days without inspection" on that morning.
    /// </summary>
    bool IsInspectionOverdue(DateOnly lastActivity, DateOnly date, int shiftDays);

    /// <summary>The first day in [<paramref name="from"/>, <paramref name="to"/>] on which the hive becomes overdue, or null.</summary>
    DateOnly? InspectionBecomesDue(DateOnly lastActivity, DateOnly from, DateOnly to, int shiftDays);

    SummaryCadence SummaryCadenceFor(SeasonPhase phase);

    /// <summary>How many days into a phase its "phase started" notice may still go out.</summary>
    int PhaseNoticeDays { get; }
}
