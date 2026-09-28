using Melarium.Application.Common.Email;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Notifications;

/// <summary>
/// How a notification looks as an e-mail (ADR-048): its icon, category and colour, the page its button
/// opens, and the footer line saying why it came. A sender with more to show — a task's due date, a
/// frost's temperature — passes its own <see cref="EmailContent"/>; this fills in what it left out, and
/// always decides the footer, because only the delivery policy knows why the mail was sent.
/// </summary>
public static class NotificationEmail
{
    public const string SecurityReason = "Sigurnosna obavještenja o vašem računu stižu uvijek, bez obzira na postavke.";
    public const string CriticalReason = "Kritična upozorenja stižu odmah, čim ih noćna provjera pronađe.";
    public const string NowReason = "Ovo stiže odmah jer je e-mail uključen za sva obavještenja.";

    public static EmailContent Compose(
        NotificationType type,
        NotificationPriority priority,
        string title,
        string message,
        int? relatedEntityId,
        string? relatedEntityType,
        EmailContent? custom,
        bool security)
    {
        var critical = priority == NotificationPriority.Critical;
        var content = custom ?? (EmailContent.Simple(title, message, Button(type, relatedEntityId, relatedEntityType))
            with { Tone = IsRemoval(type) ? EmailTone.Neutral : EmailTone.Brand });

        return content with
        {
            Icon = content.Icon ?? Icon(type),
            Eyebrow = content.Eyebrow ?? (critical && IsAlert(type) ? "Kritično upozorenje" : Category(type)),
            Tone = critical ? EmailTone.Critical : content.Tone,
            Reason = content.Reason ?? (security ? SecurityReason : critical ? CriticalReason : NowReason),
            // Security notices ignore the settings, so pointing at them would suggest otherwise.
            LinkSettings = !security,
        };
    }

    /// <summary>The same icons as the bell (<c>NotificationBell.tsx</c>), plus the types the bell leaves on its default.</summary>
    public static string Icon(NotificationType type) => type switch
    {
        NotificationType.AccountCreated                   => "🎉",
        NotificationType.OrganizationAssigned             => "🏢",
        NotificationType.OrganizationUnassigned           => "🏢",
        NotificationType.OrganizationOwnershipTransferred => "👑",
        NotificationType.ApiaryAssigned                   => "🌿",
        NotificationType.ApiaryUnassigned                 => "🌿",
        NotificationType.BeehiveAssigned                  => "🐝",
        NotificationType.BeehiveUnassigned                => "🐝",
        NotificationType.BeehiveCreated                   => "🪵",
        NotificationType.BeehiveMerged                    => "🔗",
        NotificationType.TodoCreated                      => "✅",
        NotificationType.InspectionOverdue                => "⏰",
        NotificationType.HoneyLevelDrop                   => "📉",
        NotificationType.FrostWarning                     => "❄️",
        NotificationType.OldQueen                         => "👑",
        NotificationType.WeeklySummary                    => "📰",
        NotificationType.StripsLeftIn                     => "💊",
        NotificationType.KarencaEnded                     => "🍯",
        NotificationType.FeedingOverdue                   => "🍯",
        NotificationType.TreatmentRoundOverdue            => "💊",
        NotificationType.SeasonPhaseStarted               => "🌱",
        NotificationType.LearningTopicPublished           => "🎓",
        NotificationType.LearningTopicSubmitted           => "📝",
        NotificationType.LearningTopicReviewed            => "🎓",
        NotificationType.PlanExpiring                     => "⏳",
        NotificationType.PlanLockPending                  => "⏳",
        NotificationType.DailyAgenda                      => "📅",
        NotificationType.PasswordChanged                  => "🔒",
        NotificationType.FeedbackSubmitted                => "📨",
        NotificationType.FeedbackStatusUpdated            => "💬",
        NotificationType.InvitationAccepted               => "🤝",
        _                                                 => "🔔",
    };

    /// <summary>The small label above the title.</summary>
    public static string Category(NotificationType type) => type switch
    {
        NotificationType.AccountCreated => "Dobrodošli",
        NotificationType.PasswordChanged => "Sigurnost računa",
        NotificationType.OrganizationAssigned
            or NotificationType.OrganizationUnassigned
            or NotificationType.OrganizationOwnershipTransferred => "Organizacija",
        NotificationType.ApiaryAssigned
            or NotificationType.ApiaryUnassigned
            or NotificationType.BeehiveAssigned
            or NotificationType.BeehiveUnassigned => "Dodjela",
        NotificationType.BeehiveCreated or NotificationType.BeehiveMerged => "Pčelinjak",
        NotificationType.TodoCreated => "Zadatak",
        NotificationType.WeeklySummary => "Pregled",
        NotificationType.SeasonPhaseStarted => "Sezona",
        NotificationType.LearningTopicPublished
            or NotificationType.LearningTopicSubmitted
            or NotificationType.LearningTopicReviewed => "Edukacija",
        NotificationType.PlanExpiring or NotificationType.PlanLockPending => "Paket",
        NotificationType.DailyAgenda => "Obaveze",
        NotificationType.FeedbackSubmitted or NotificationType.FeedbackStatusUpdated => "Podrška",
        NotificationType.InvitationAccepted => "Pozivnice",
        _ when IsAlert(type) => "Upozorenje",
        _ => "Obavještenje",
    };

    /// <summary>
    /// Where the button goes, from what the notification is about. Pages a Beekeeper cannot open
    /// (the organization page) are never the target — "/" is.
    /// </summary>
    public static EmailLink Button(NotificationType type, int? relatedEntityId, string? relatedEntityType) =>
        (relatedEntityType, relatedEntityId) switch
        {
            ("Beehive", int id) => new($"/beehives/{id}", "Otvori košnicu"),
            ("Apiary", int id) => new($"/apiaries/{id}", "Otvori pčelinjak"),
            ("Treatment", int id) => new($"/treatments/{id}", "Otvori tretman"),
            ("Diet", int id) => new($"/feedings/{id}", "Otvori prehranu"),
            ("Organization", _) when type is NotificationType.PlanExpiring or NotificationType.PlanLockPending
                => new("/plans", "Pogledaj pakete"),
            ("Feedback", _) => new("/profile#povratne-informacije", "Moje povratne informacije"),
            ("LearningTopic", _) => new("/learning/moje-teme", "Moje teme"),
            ("Invitation", _) => new("/invite", "Pozivnice"),
            ("SeasonPhase", _) => new("/learning?category=2", "Sezonski radovi u Edukaciji"),
            ("DailyAgenda", _) => new("/calendar", "Otvori kalendar"),
            _ => new("/", "Otvori Melarium"),
        };

    /// <summary>The scan's alerts — "Kritično upozorenje" when one is Critical.</summary>
    public static bool IsAlert(NotificationType type) => type is
        NotificationType.InspectionOverdue or NotificationType.HoneyLevelDrop or NotificationType.FrostWarning or
        NotificationType.OldQueen or NotificationType.StripsLeftIn or NotificationType.KarencaEnded or
        NotificationType.FeedingOverdue or NotificationType.TreatmentRoundOverdue;

    private static bool IsRemoval(NotificationType type) => type is
        NotificationType.OrganizationUnassigned or NotificationType.ApiaryUnassigned or NotificationType.BeehiveUnassigned;
}
