namespace Melarium.Domain.Enums;

/// <summary>What kind of change an announcement describes (SPEC-21 D7). Bosnian labels live in the
/// frontend label map, like <see cref="LearningCategory"/>.</summary>
public enum AnnouncementType
{
    New         = 1,
    Improvement = 2,
    Fix         = 3,
}
