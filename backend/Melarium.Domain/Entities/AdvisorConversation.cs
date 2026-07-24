using Melarium.Domain.Common;

namespace Melarium.Domain.Entities;

/// <summary>
/// A personal AI-advisor chat thread owned by one user. Optionally bound to a beehive, in which case
/// the assistant's answers are grounded in that hive's real data. Conversations are private — never
/// shared across the organization.
/// </summary>
public class AdvisorConversation : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>The hive this conversation is about, or null for a general question. SET NULL on hive delete.</summary>
    public int? BeehiveId { get; set; }
    public Beehive? Beehive { get; set; }

    /// <summary>Auto-generated from the first user message (~60 chars).</summary>
    public string Title { get; set; } = string.Empty;

    public List<AdvisorMessage> Messages { get; set; } = [];
}
