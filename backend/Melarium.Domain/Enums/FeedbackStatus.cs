namespace Melarium.Domain.Enums;

/// <summary>Triage state of a feedback submission (SPEC-13). Only SystemAdmin can change it.</summary>
public enum FeedbackStatus
{
    New       = 1,
    InReview  = 2,
    Resolved  = 3,
    Dismissed = 4,
}
