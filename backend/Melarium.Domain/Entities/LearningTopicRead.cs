namespace Melarium.Domain.Entities;

using Melarium.Domain.Common;

/// <summary>Per-user read marker for a learning topic — unique per (Topic, User).</summary>
public class LearningTopicRead : BaseEntity
{
    public int TopicId { get; set; }
    public LearningTopic Topic { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
