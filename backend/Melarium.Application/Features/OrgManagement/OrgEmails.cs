using Melarium.Application.Common.Email;

namespace Melarium.Application.Features.OrgManagement;

/// <summary>Organization handover, as an e-mail (ADR-048).</summary>
public static class OrgEmails
{
    /// <summary>What changed, then the one thing to do — sign in again — in its own box.</summary>
    public static EmailContent OwnershipTransferred(string handover, string signInAgain) =>
        new("Postali ste administrator organizacije")
        {
            Blocks = [new EmailText(handover), new EmailCallout(signInAgain, EmailTone.Brand, "💡")],
            Button = new EmailLink("/organization", "Otvori organizaciju"),
        };
}
