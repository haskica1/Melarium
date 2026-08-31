namespace Melarium.Application.Features.Profile.DTOs;

/// <summary>
/// What deleting this account would actually do, resolved on the server so the confirmation screen
/// never has to re-derive the rule. The client renders whichever of the three outcomes it is told.
/// </summary>
public class AccountDeletionPreviewDto
{
    /// <summary>
    /// <c>account</c> — only the caller's own account and personal records go.
    /// <c>organization</c> — the caller is the last member, so the organization and everything in it
    /// goes with them; the confirmation must ask them to type <see cref="OrganizationName"/>.
    /// <c>transfer-required</c> — the caller is the OrganizationAdmin of an organization that still
    /// has members, so nothing can be deleted until ownership is handed over.
    /// </summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>The organization's name — null for a SystemAdmin, who belongs to none.</summary>
    public string? OrganizationName { get; set; }

    /// <summary>Members in the organization, the caller included.</summary>
    public int MemberCount { get; set; }

    /// <summary>Apiaries that would be destroyed. Only meaningful when <see cref="Mode"/> is <c>organization</c>.</summary>
    public int ApiaryCount { get; set; }

    /// <summary>Beehives that would be destroyed. Only meaningful when <see cref="Mode"/> is <c>organization</c>.</summary>
    public int BeehiveCount { get; set; }

    /// <summary>
    /// True when the deletion takes the organization's treatment register with it. Surfaced on its
    /// own rather than inferred from <see cref="Mode"/>, because it is the one loss the user is
    /// legally required to keep records of (SPEC-08) and it has to be said out loud in the dialog.
    /// </summary>
    public bool DeletesTreatmentRegister { get; set; }
}
