using Melarium.Application.Common.Email;
using Melarium.Application.Features.Calendar;
using Melarium.Application.Features.Reminders;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// The structure of the one morning e-mail (SPEC-29, ADR-048): numbers, "Traži pažnju" cards, today's
/// checklist, what there is to read — and the Bosnian that has to agree with every count.
/// </summary>
public class MorningEmailTests
{
    private static readonly DateOnly Monday = new(2026, 4, 20);

    private static Notification Alert(NotificationType type, string title, string message,
        int? entityId = 2, string? entityType = "Apiary") =>
        new() { Type = type, Title = title, Message = message, RelatedEntityId = entityId, RelatedEntityType = entityType };

    private static CalendarObligation Obligation(string title, string? location = null, string? path = null,
        ObligationKind kind = ObligationKind.Todo) =>
        new(kind, title, Monday, title, null, location, null, 2, false, path);

    private static readonly Notification Overdue = Alert(NotificationType.InspectionOverdue, "Košnice bez pregleda",
        "Pčelinjak 'Visoko' (3 košnice):\n- K2 (34 dana)\n- K5 (29 dana)\n- K8 (još nije pregledana)");

    private static readonly Notification Honey = Alert(NotificationType.HoneyLevelDrop, "Opada nivo meda",
        "Košnici 'K3' (pčelinjak 'Ozren') opada nivo meda — razmisli o prehrani.");

    private static readonly Notification Phase = Alert(NotificationType.SeasonPhaseStarted, "Počinje glavna sezona",
        "Traje od 16.04. do 31.07. — radovi za ovu fazu:\n- Pregledi svakih 7–10 dana\n- Mjere protiv rojenja\n\nIz Edukacije:\n- Rojenje",
        entityId: 20263, entityType: "SeasonPhase");

    private static readonly Notification Summary = Alert(NotificationType.WeeklySummary, "Sedmični pregled",
        "- Obavljeno je 14 pregleda.\n- Završene su 2 runde prehrane.", entityId: 1, entityType: "Organization");

    [Fact]
    public void Everything_ComesInTheOrder_NumbersAttentionTodayReading()
    {
        var mail = MorningEmail.Compose(Monday,
            [Obligation("🍯 Prehrana — Ozren (8 košnica)", "Ozren", "/feedings/3"), Obligation("📋 Zamijeniti okvire — Visoko", "Visoko")],
            [Overdue, Phase, Honey, Summary], "Amra");

        Assert.Equal("Dobro jutro, Amra", mail.Title);
        Assert.False(mail.Greet);
        Assert.Equal("Jutarnji pregled · ponedjeljak, 20. april", mail.Eyebrow);
        Assert.Equal("Jutarnji pregled: 2 obaveze, 2 upozorenja", mail.Subject);
        Assert.Equal("Danas te čekaju 2 obaveze, a 2 stvari traže pažnju.", Assert.IsType<EmailText>(mail.Blocks[0]).Text);
        Assert.IsType<EmailStats>(mail.Blocks[1]);
        Assert.Equal(new[] { "Traži pažnju", "Današnje obaveze", "Za čitanje" },
            mail.Blocks.OfType<EmailSection>().Select(s => s.Title));
        Assert.Equal("/", mail.Button!.Url);
        Assert.True(mail.LinkSettings);
    }

    [Fact]
    public void GroupedAlert_ListsItsHivesAsRows()
    {
        var card = MorningEmail.Compose(Monday, [], [Overdue], "Amra").Blocks.OfType<EmailCard>().Single();

        Assert.Equal("Pčelinjak 'Visoko' (3 košnice)", card.Subtitle);
        Assert.Equal(new[] { ("K2", "34 dana"), ("K5", "29 dana"), ("K8", "još nije pregledana") },
            card.Rows.Select(r => (r.Name, r.Detail!)));
        Assert.Equal("/apiaries/2", card.Link!.Url);
        Assert.Equal(EmailTone.Warning, card.Tone);
    }

    [Fact]
    public void OneLineAlert_IsItsSentence()
    {
        var card = MorningEmail.Compose(Monday, [], [Honey], "Amra").Blocks.OfType<EmailCard>().Single();

        Assert.Equal(Honey.Message, card.Text);
        Assert.Empty(card.Rows);
    }

    [Fact]
    public void PhaseNotice_IsTheFeaturedCard_WithItsEdukacijaList()
    {
        var card = MorningEmail.Compose(Monday, [], [Phase], "Amra").Blocks.OfType<EmailCard>().Single();

        Assert.True(card.Featured);
        Assert.Equal("Traje od 16.04. do 31.07. — radovi za ovu fazu", card.Subtitle);
        Assert.Equal(new[] { "Pregledi svakih 7–10 dana", "Mjere protiv rojenja" }, card.Bullets);
        Assert.Equal("Iz Edukacije", card.MoreHeading);
        Assert.Equal(new[] { "Rojenje" }, card.MoreBullets);
        Assert.Equal("/learning?category=2", card.Link!.Url);
    }

    [Fact]
    public void AiSummary_IsTaggedAndKeepsItsBullets()
    {
        var card = MorningEmail.Compose(Monday, [], [Summary], "Amra").Blocks.OfType<EmailCard>().Single();

        Assert.Equal("AI", card.Tag);
        Assert.Equal(2, card.Bullets.Count);
    }

    [Fact]
    public void Obligation_SplitsWhatFromWhere_AtItsLocation()
    {
        // The dash inside the todo's own title stays with the title.
        var mail = MorningEmail.Compose(Monday,
            [Obligation("📋 Zamijeniti okvire — hitno — Visoko", "Visoko", "/apiaries/2")], [], "Amra");

        var row = mail.Blocks.OfType<EmailChecklist>().Single().Items.Single();
        Assert.Equal("📋", row.Icon);
        Assert.Equal("Zamijeniti okvire — hitno", row.Name);
        Assert.Equal("Visoko", row.Detail);
        Assert.Equal("/apiaries/2", row.Url);
    }

    [Theory]
    [InlineData(1, 0, "Danas te čeka 1 obaveza.")]
    [InlineData(4, 0, "Danas te čekaju 4 obaveze.")]
    [InlineData(5, 1, "Danas te čeka 5 obaveza, a 1 stvar traži pažnju.")]
    [InlineData(0, 2, "Danas nema obaveza u kalendaru, ali 2 stvari traže pažnju.")]
    [InlineData(0, 11, "Danas nema obaveza u kalendaru, ali 11 stvari traži pažnju.")]
    public void Lead_AgreesWithTheCounts(int obligations, int alerts, string expected)
    {
        var mail = MorningEmail.Compose(Monday,
            Enumerable.Range(0, obligations).Select(i => Obligation($"📋 Zadatak {i}")).ToList(),
            Enumerable.Range(0, alerts).Select(_ => Honey).ToList(), "Amra");

        Assert.Equal(expected, Assert.IsType<EmailText>(mail.Blocks[0]).Text);
        // The numbers only when there are two to compare.
        Assert.Equal(obligations > 0 && alerts > 0, mail.Blocks.OfType<EmailStats>().Any());
    }

    [Fact]
    public void OnlySomethingToRead_NamesItInTheSubject()
    {
        var mail = MorningEmail.Compose(Monday, [], [Phase], null);

        Assert.Equal("Dobro jutro", mail.Title);
        Assert.Equal("Jutarnji pregled: Počinje glavna sezona", mail.Subject);
    }
}
