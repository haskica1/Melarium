using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>AI-assistant session data access (SPEC-17).</summary>
public interface IAiAssistantSessionRepository : IRepository<AiAssistantSession>
{
    /// <summary>A user's sessions, newest activity first, without turn rows.</summary>
    Task<IEnumerable<AiAssistantSession>> GetByUserAsync(int userId);

    /// <summary>A single session with its turns and their actions (ordered) — tracked, for appends.</summary>
    Task<AiAssistantSession?> GetWithTurnsAsync(int id);

    /// <summary>One turn with its actions and owning session — tracked, for the confirm path.</summary>
    Task<AiAssistantTurn?> GetTurnWithActionsAsync(int turnId);

    /// <summary>
    /// Number of user turns issued by the organization's members since the given UTC instant —
    /// the per-organization monthly assistant quota (SPEC-09 precedent).
    /// </summary>
    Task<int> CountUserTurnsForOrganizationSinceAsync(int organizationId, DateTime sinceUtc);
}
