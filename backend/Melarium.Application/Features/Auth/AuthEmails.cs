using Melarium.Application.Common.Email;
using Melarium.Application.Common.Localization;

namespace Melarium.Application.Features.Auth;

/// <summary>
/// The account e-mails (ADR-048): the two one-time links, the two welcomes, and the password-changed
/// notice. Shared by <see cref="AuthService"/>, <c>ProfileService</c> and the two places that open an
/// account for someone else, so the same event reads the same wherever it starts.
/// </summary>
public static class AuthEmails
{
    public static EmailContent VerifyEmail(string url, int hours) => new("Potvrdite vašu e-poštu")
    {
        Eyebrow = "Račun",
        Icon = "✉️",
        Blocks =
        [
            new EmailText("Da biste primali obavijesti i mogli vratiti pristup računu ako zaboravite lozinku, "
                + "potvrdite da je ova adresa vaša."),
        ],
        Button = new EmailLink(url, "Potvrdi e-poštu"),
        ShowLinkFallback = true,
        After =
        [
            new EmailCallout($"Link vrijedi {Hours(hours)}. Ako niste vi tražili ovu poruku, slobodno je zanemarite.",
                EmailTone.Neutral, "ℹ️"),
        ],
        Reason = "Ovaj e-mail je poslan jer je ova adresa upisana na računu u Melariumu.",
    };

    public static EmailContent PasswordReset(string url, int hours) => new("Zahtjev za promjenu lozinke")
    {
        Eyebrow = "Sigurnost računa",
        Icon = "🔑",
        Blocks = [new EmailText("Primili smo zahtjev za promjenu lozinke na vašem računu. Kliknite dugme i postavite novu.")],
        Button = new EmailLink(url, "Postavi novu lozinku"),
        ShowLinkFallback = true,
        After =
        [
            new EmailCallout($"Link vrijedi {Hours(hours)} i radi samo jednom. Ako niste vi tražili promjenu, "
                + "zanemarite ovu poruku — lozinka ostaje ista.", EmailTone.Neutral, "ℹ️"),
        ],
        Reason = "Ovaj e-mail je poslan jer je promjena lozinke zatražena u Melariumu.",
    };

    /// <summary>Whoever registered: they own a new, empty organization.</summary>
    public static EmailContent WelcomeOwner(string organizationName) => new("Dobrodošli u Melarium!")
    {
        Blocks =
        [
            new EmailText($"Vaša organizacija '{organizationName}' je spremna. Vi ste njen administrator — "
                + "počnite dodavanjem prvog pčelinjaka."),
            new EmailSection("Prva tri koraka"),
            new EmailChecklist(
            [
                new EmailRow("Dodajte pčelinjak", "Lokacija donosi i vremensku prognozu i upozorenja na mraz", "🌿"),
                new EmailRow("Upišite košnice", "Svaku možete označiti QR kodom za brzi pregled", "🐝"),
                new EmailRow("Zabilježite prvi pregled", "Upišite ga rukom ili izdiktirajte glasom", "🔍"),
            ]),
        ],
        Button = new EmailLink("/apiaries/new", "Dodaj prvi pčelinjak"),
        Reason = "Ovaj e-mail je poslan jer ste upravo otvorili račun u Melariumu.",
    };

    /// <summary>Someone an admin created an account for — they need to know which address signs in.</summary>
    public static EmailContent WelcomeMember(string email) => new("Dobrodošli u Melarium!")
    {
        Blocks =
        [
            new EmailText("Vaš račun je kreiran. Prijavite se s ovom adresom e-pošte:"),
            new EmailFacts([new EmailFact("E-pošta", email)]),
        ],
        Button = new EmailLink("/login", "Prijavi se"),
        Reason = "Ovaj e-mail je poslan jer vam je administrator otvorio račun u Melariumu.",
    };

    /// <summary>After a reset or a change; the "when" is the send time, seconds after the change.</summary>
    public static EmailContent PasswordChanged() => new("Lozinka je promijenjena")
    {
        Blocks =
        [
            new EmailText("Vaša lozinka je uspješno promijenjena i odjavljeni ste sa svih uređaja."),
            new EmailFacts([new EmailFact("Kada", SentAt: true)]),
            new EmailCallout("Niste vi mijenjali lozinku? Odmah zatražite novu i javite nam se na info@melarium.app.",
                EmailTone.Critical, "⚠️"),
        ],
        Button = new EmailLink("/forgot-password", "Nisam ja — zaštiti račun"),
    };

    private static string Hours(int hours) => BsLabels.Count(hours, "sat", "sata", "sati");
}
