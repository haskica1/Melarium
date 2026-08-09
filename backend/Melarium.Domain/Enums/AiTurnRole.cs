namespace Melarium.Domain.Enums;

/// <summary>
/// Who authored an AI-assistant turn (SPEC-17). Deliberately separate from <see cref="AdvisorRole"/>:
/// the values coincide today, but a shared enum would hand any future advisor role to the assistant.
/// </summary>
public enum AiTurnRole
{
    User      = 1,
    Assistant = 2,
}
