namespace Melarium.Domain.Entities;

using Melarium.Domain.Common;

/// <summary>
/// Per-user "seen" marker for an announcement — unique per (Announcement, User).
/// </summary>
/// <remarks>
/// One state, not two (SPEC-21 D2): this row is written both when the user dismisses the banner with
/// "x" and when they close the modal after reading it. A user who read the whole text needs no
/// further banner, so a separate "dismissed" flag would only add a way for the two to disagree.
/// </remarks>
public class AnnouncementRead : BaseEntity
{
    public int AnnouncementId { get; set; }
    public Announcement Announcement { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
