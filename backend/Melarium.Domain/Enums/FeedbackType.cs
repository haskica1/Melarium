namespace Melarium.Domain.Enums;

/// <summary>
/// What kind of message the user is sending (SPEC-13). Deliberately wider than bug reports —
/// praise and complaints are as useful to act on as defects, and a user who only has a question
/// needs somewhere to put it that is not a bug report.
/// </summary>
public enum FeedbackType
{
    Bug            = 1,
    Complaint      = 2,
    Compliment     = 3,
    FeatureRequest = 4,
    Question       = 5,
    Other          = 6,
}
