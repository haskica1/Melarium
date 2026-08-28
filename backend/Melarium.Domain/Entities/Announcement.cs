using Melarium.Domain.Common;
using Melarium.Domain.Enums;

namespace Melarium.Domain.Entities;

/// <summary>
/// One product announcement — "Šta je novo" (SPEC-21). Platform-wide content authored by SystemAdmin,
/// visible to every user once published. Deliberately separate from <see cref="LearningTopic"/>:
/// Edukacija is beekeeping knowledge, this is news about the app.
/// </summary>
public class Announcement : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    /// <summary>The announcement body, markdown. There is no separate summary field — the banner
    /// derives its subtitle from this (SPEC-21 D6).</summary>
    public string BodyMarkdown { get; set; } = string.Empty;

    public AnnouncementType Type { get; set; }

    public bool IsPublished { get; set; }

    /// <summary>
    /// Set on the first publish only — the guard that stops an edit from resurrecting a dismissed
    /// banner (D9), and the sort key for the whole feature (D10: a draft written in January and
    /// published in March is a March announcement).
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    public List<AnnouncementRead> Reads { get; set; } = [];
}
