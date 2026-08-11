using Melarium.Domain.Common;

namespace Melarium.Domain.Entities;

/// <summary>
/// One AI-assistant thread owned by a single user (SPEC-17). Sessions are private — another user's
/// session is a 404, never a 403, so the API is not an existence oracle.
/// </summary>
public class AiAssistantSession : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>The hive this session is about, if any (SPEC-18). SET NULL on hive delete.</summary>
    public int? BeehiveId { get; set; }
    public Beehive? Beehive { get; set; }

    /// <summary>Auto-generated from the first user message (~60 chars).</summary>
    public string Title { get; set; } = string.Empty;

    public List<AiAssistantTurn> Turns { get; set; } = [];
}
