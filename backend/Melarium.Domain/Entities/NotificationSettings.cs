using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

/// <summary>
/// Per-user notification preferences (SPEC-29), shaped after <see cref="CalendarSettings"/>: exactly
/// one row per user, created lazily on the first save. A user without a row gets the defaults below,
/// which is what every account had before this existed — all e-mail, every alert in the app.
/// </summary>
public class NotificationSettings : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public EmailNotificationMode EmailMode { get; set; } = EmailNotificationMode.All;

    // Only scan alerts are switchable. Critical has no switch on purpose, and notifications a person
    // triggers (a todo, an assignment) are not alerts at all.
    public bool NormalAlertsInApp { get; set; } = true;
    public bool InfoAlertsInApp { get; set; } = true;
}
