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

    // ── Colony merge (SPEC-19 D6) — same audience and reason as BeehiveCreated: the hive count
    // of an apiary changed, and the person responsible for it did not do it themselves.
    BeehiveMerged          = 26,

    // ── Organization ownership handover — the successor did not ask for this and has to be told
    // that an entire organization is now theirs to run.
    OrganizationOwnershipTransferred = 27,

    // ── Downgrade lock (SPEC-24) — two days before a plan expires, the warning that data is about
    // to become unreachable. Distinct from PlanExpiring: that one says the plan is ending, this one
    // says what specifically stops opening, and it is only sent to organizations that actually lose
    // something. An organization inside its limits never sees it.
    PlanLockPending = 28,
}
