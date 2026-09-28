using Melarium.Application.Common.Email;
using Melarium.Domain.Entities;

namespace Melarium.Application.Features.Alerts;

/// <summary>
/// The alerts that can go out on their own, as e-mails (ADR-048). Only a Critical one is mailed at
/// once — frost in spring and the main season, the data lock — the rest travel in the morning e-mail,
/// which draws them from the stored notification instead.
/// </summary>
public static class AlertEmails
{
    public static EmailContent Frost(string title, Apiary apiary, double minTemp, string advice)
    {
        // A real minus sign: in a subject line a hyphen reads as a dash.
        var temperature = $"{minTemp:0.#} °C".Replace('-', '−');
        return new EmailContent(title)
        {
            Subject = $"❄️ {title} — {apiary.Name}, {temperature}",
            Blocks =
            [
                new EmailFacts(
                [
                    new EmailFact("Pčelinjak", apiary.Name),
                    new EmailFact("Najniža temperatura", temperature),
                    new EmailFact("Kada", "danas ili sutra"),
                ]),
                new EmailText(advice),
            ],
            Button = new EmailLink($"/apiaries/{apiary.Id}", "Otvori pčelinjak"),
        };
    }

    public static EmailContent PlanLock(string plan, DateTime validUntil, string lockSummary) => new("Dio podataka postaje nedostupan")
    {
        Subject = $"Paket ističe {validUntil:dd.MM.} — dio podataka postaje nedostupan",
        Blocks =
        [
            new EmailText("Vaš paket uskoro ističe. Nakon toga dio podataka ostaje nedostupan dok ga ne produžite."),
            new EmailFacts(
            [
                new EmailFact("Paket", plan),
                new EmailFact("Ističe", $"{validUntil:dd.MM.yyyy.}"),
                new EmailFact("Postaje nedostupno", lockSummary),
            ]),
            new EmailCallout("Podaci se ne brišu — sve se vraća čim produžite paket.", EmailTone.Success, "🛡️"),
        ],
        Button = new EmailLink("/plans", "Produži paket"),
    };
}
