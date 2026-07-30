namespace Melarium.Domain.Enums;

/// <summary>
/// How badly a problem affects the user (SPEC-13). Optional on every submission and only
/// meaningful for <see cref="FeedbackType.Bug"/> / <see cref="FeedbackType.Complaint"/> — a
/// compliment has no severity.
/// </summary>
public enum FeedbackSeverity
{
    Low      = 1,
    Medium   = 2,
    High     = 3,
    Critical = 4,
}
