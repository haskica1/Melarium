namespace Melarium.Domain.Enums;

public enum NotificationType
{
    AccountCreated         = 1,
    OrganizationAssigned   = 2,
    OrganizationUnassigned = 3,
    ApiaryAssigned         = 4,
    ApiaryUnassigned       = 5,
    BeehiveAssigned        = 6,
    BeehiveUnassigned      = 7,
    BeehiveCreated         = 8,
    TodoCreated            = 9,

    // ── Smart alerts & weekly summary (SPEC-04) ──
    InspectionOverdue      = 10,
    HoneyLevelDrop         = 11,
    FrostWarning           = 12,
    OldQueen               = 13,
    WeeklySummary          = 14,

    // ── Treatment register (SPEC-08) ──
    StripsLeftIn           = 15,
    KarencaEnded           = 16,

    // ── Learning module (SPEC-06) — 15 was taken by SPEC-08, so 17 ──
    LearningTopicPublished = 17,

    // ── Plans & billing (SPEC-09) ──
    PlanExpiring           = 18,

    // ── Calendar sync (SPEC-11) — daily 08:00 agenda of the day's obligations ──
    DailyAgenda            = 19,

    // ── Account security — password reset/change; the user's cue that it wasn't them ──
    PasswordChanged        = 20,

    // ── User feedback (SPEC-13) ──
    // FeedbackSubmitted is in-app only for SystemAdmins (the email goes to Feedback:NotifyEmail
    // instead, so it is sent once rather than once per admin).
    FeedbackSubmitted      = 21,
    FeedbackStatusUpdated  = 22,

    // ── Apiary feeding (SPEC-12 Phase D) ──
    FeedingOverdue         = 23,

    // ── Invite a friend (SPEC-15) ──
    // Two moments share one type: the invitee registering, and the reward landing once they
    // verify their e-mail. Both are about the same invitation, so they are one kind of news.
    InvitationAccepted     = 24,

    // ── Treatment application rounds — parity with FeedingOverdue ──
    TreatmentRoundOverdue  = 25,
}
