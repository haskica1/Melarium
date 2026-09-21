namespace Melarium.Domain.Enums;

/// <summary>
/// Review state of a learning topic that a user proposed (SPEC-26). <see cref="None"/> is the
/// default and means the topic never went through review — that is every SystemAdmin-authored
/// topic, including all content that predates this feature.
/// </summary>
public enum TopicReviewStatus
{
    None     = 0,
    Pending  = 1,
    Approved = 2,
    Rejected = 3,
}
