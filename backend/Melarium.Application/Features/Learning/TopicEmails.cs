using Melarium.Application.Common.Email;
using Melarium.Domain.Entities;

namespace Melarium.Application.Features.Learning;

/// <summary>The platform's answer to a topic's author (ADR-048), either way.</summary>
public static class TopicEmails
{
    public static EmailContent Published(LearningTopic topic, string text) => new("Vaša tema je objavljena")
    {
        Subject = $"Objavljeno: {topic.Title}",
        Tone = EmailTone.Success,
        Blocks = [new EmailText(text)],
        Button = new EmailLink($"/learning/{topic.Id}", "Pročitaj temu"),
    };

    /// <summary>The reason in its own box — it is what the author has to act on.</summary>
    public static EmailContent Rejected(LearningTopic topic) => new("Vaša tema nije objavljena")
    {
        Subject = $"Tema nije objavljena: {topic.Title}",
        Tone = EmailTone.Neutral,
        Blocks =
        [
            new EmailText($"Tema \"{topic.Title}\" nije odobrena."),
            new EmailCallout($"Razlog: {topic.RejectionReason}", EmailTone.Warning, "📝"),
            new EmailText("Možete je doraditi i poslati ponovo."),
        ],
        Button = new EmailLink($"/learning/moje-teme/{topic.Id}/uredi", "Doradi temu"),
    };
}
