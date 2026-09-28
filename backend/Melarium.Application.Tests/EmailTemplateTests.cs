using Melarium.Application.Common.Email;
using Melarium.Infrastructure.Email;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// The one e-mail template (ADR-048): user text never becomes markup, paths become links on the app,
/// the greeting and the footer follow the content's flags, and nothing is fetched from Google on open.
/// </summary>
public class EmailTemplateTests
{
    private static readonly EmailContext Ctx =
        new("https://melarium.app", "https://melarium.app/pwa-192x192.png", "Amra", new DateTime(2026, 4, 20, 9, 42, 0));

    [Fact]
    public void UserText_IsEscaped_Everywhere()
    {
        var html = EmailTemplate.Render(new EmailContent("<b>Naslov</b>")
        {
            Blocks =
            [
                new EmailText("<script>alert(1)</script>"),
                new EmailFacts([new EmailFact("Tema", "<img src=x onerror=alert(1)>")]),
                new EmailCard("<i>K1</i>") { Rows = [new EmailRow("<u>K2</u>", "34 dana")] },
                new EmailCallout("<a href=\"https://evil\">klik</a>"),
            ],
        }, Ctx);

        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img src=x", html);
        Assert.DoesNotContain("<b>Naslov", html);
        Assert.DoesNotContain("<i>K1", html);
        Assert.DoesNotContain("<u>K2", html);
        Assert.DoesNotContain("href=\"https://evil\"", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void Paths_ResolveAgainstTheApp_AbsoluteLinksStayAsTheyAre()
    {
        var html = EmailTemplate.Render(new EmailContent("Naslov")
        {
            Button = new EmailLink("/beehives/7", "Otvori košnicu"),
            Blocks = [new EmailCard("K1") { Link = new EmailLink("https://example.com/x?a=1&b=2", "Vanjski") }],
        }, Ctx);

        Assert.Contains("href=\"https://melarium.app/beehives/7\"", html);
        Assert.Contains("href=\"https://example.com/x?a=1&amp;b=2\"", html);
    }

    [Fact]
    public void Greeting_UsesTheFirstName_OnlyWhenTheContentGreets()
    {
        Assert.Contains("Pozdrav, Amra", EmailTemplate.Render(new EmailContent("Naslov"), Ctx));
        Assert.DoesNotContain("Pozdrav,", EmailTemplate.Render(new EmailContent("Naslov") { Greet = false }, Ctx));
        Assert.DoesNotContain("Pozdrav,", EmailTemplate.Render(new EmailContent("Naslov"), Ctx with { FirstName = null }));
    }

    [Fact]
    public void SentAtFact_PrintsTheLocalSendTime()
    {
        var html = EmailTemplate.Render(new EmailContent("Naslov")
        {
            Blocks = [new EmailFacts([new EmailFact("Kada", SentAt: true)])],
        }, Ctx);

        Assert.Contains("ponedjeljak, 20.04.2026. u 09:42", html);
    }

    [Fact]
    public void Footer_FollowsTheFlags()
    {
        var notification = EmailTemplate.Render(new EmailContent("Naslov") { LinkSettings = true }, Ctx);
        Assert.Contains("https://melarium.app/profile#obavjestenja", notification);
        Assert.Contains("info@melarium.app", notification);

        var operatorMail = EmailTemplate.Render(new EmailContent("Naslov") { Internal = true }, Ctx);
        Assert.DoesNotContain("profile#obavjestenja", operatorMail);
        Assert.DoesNotContain("Trebate pomoć", operatorMail);
    }

    [Fact]
    public void NothingIsLoadedFromGoogle_OnOpen()
    {
        // An @import would tell Google the reader's IP every time the mail is opened.
        var html = EmailTemplate.Render(new EmailContent("Naslov"), Ctx);

        Assert.DoesNotContain("fonts.googleapis.com", html);
        Assert.DoesNotContain("@import", html);
    }

    [Fact]
    public void TextPart_HasTheContentAndTheLinks_WithoutMarkup()
    {
        var text = EmailTemplate.RenderText(new EmailContent("Nova košnica")
        {
            Blocks = [new EmailText("Dodana je košnica 'K13'."), new EmailFacts([new EmailFact("Pčelinjak", "Visoko")])],
            Button = new EmailLink("/beehives/13", "Otvori košnicu"),
            LinkSettings = true,
        }, Ctx);

        Assert.Contains("Pozdrav, Amra", text);
        Assert.Contains("Dodana je košnica 'K13'.", text);
        Assert.Contains("Pčelinjak: Visoko", text);
        Assert.Contains("Otvori košnicu: https://melarium.app/beehives/13", text);
        Assert.Contains("https://melarium.app/profile#obavjestenja", text);
        Assert.DoesNotContain("<", text);
    }
}
