using Melarium.Application.Common.Email;
using Melarium.Application.Common.Localization;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Feedbacks;

/// <summary>The two feedback e-mails (ADR-048): the operator's copy and the answer to the submitter.</summary>
public static class FeedbackEmails
{
    /// <summary>
    /// The operator's copy: the report's subject as the title, who/where/how bad as rows, the text
    /// itself in a box, and a button to the admin list. Severity goes into the subject tag.
    /// </summary>
    public static EmailContent Operator(Feedback feedback, string typeLabel)
    {
        var facts = new List<EmailFact> { new("Vrsta", typeLabel) };
        if (feedback.Severity is FeedbackSeverity severity) facts.Add(new("Ozbiljnost", BsLabels.Label(severity)));
        facts.Add(new("Poslao", feedback.UserId is int uid ? $"korisnik #{uid}" : "nepoznat korisnik"));
        if (feedback.PageContext is { Length: > 0 } page) facts.Add(new("Stranica", page));

        var tag = feedback.Severity is FeedbackSeverity s ? $"{typeLabel} · {BsLabels.Label(s).ToLowerInvariant()}" : typeLabel;
        var urgent = feedback.Severity is FeedbackSeverity.High or FeedbackSeverity.Critical;

        return new EmailContent(feedback.Subject)
        {
            Subject = $"[{tag}] {feedback.Subject}",
            Eyebrow = "Nova povratna informacija",
            Icon = feedback.Type == FeedbackType.Bug ? "🐞" : "📨",
            Tone = urgent ? EmailTone.Warning : EmailTone.Brand,
            Greet = false,
            Internal = true,
            Blocks = [new EmailFacts(facts), new EmailCallout(feedback.Message)],
            Button = new EmailLink("/admin/feedback", "Otvori u adminu"),
            Reason = "Interna poruka — stiže na adresu iz Feedback:NotifyEmail.",
        };
    }

    /// <summary>The submitter's answer: topic and status as rows, the reply in a box, green once resolved.</summary>
    public static EmailContent Reply(Feedback feedback)
    {
        var status = BsLabels.Label(feedback.Status);
        List<EmailBlock> blocks = [new EmailFacts([new("Tema", feedback.Subject), new("Status", status)])];
        if (feedback.AdminResponse is { Length: > 0 } reply) blocks.Add(new EmailCallout(reply, EmailTone.Brand, "💬"));

        return new EmailContent("Odgovor na vašu povratnu informaciju")
        {
            Subject = $"Odgovor: {feedback.Subject} — {status.ToLowerInvariant()}",
            Tone = feedback.Status == FeedbackStatus.Resolved ? EmailTone.Success : EmailTone.Brand,
            Blocks = blocks,
            Button = new EmailLink("/profile#povratne-informacije", "Moje povratne informacije"),
        };
    }
}
