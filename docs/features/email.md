# Feature: E-mails (the one template)

> Redesigned 2026-09-28. Decision: ADR-048. Delivery rules (who gets what, when) are ADR-047 and
> [`seasonal-notifications.md`](seasonal-notifications.md); this document is about what a mail
> contains and how it looks.

## Shape of every mail

App icon and "Melarium" wordmark → a card whose **tinted header** carries an icon tile, a small
upper-case category ("Zadatak", "Kritično upozorenje") and the title → the body → one button → a
footer that says **why this mail came** and links to the notification settings.

| Tone | Header | Used for |
|---|---|---|
| Brand (honey) | `#fff7e6` | everyday notifications, account mails |
| Critical (red) | `#fff1f1` | Critical alerts (frost in spring/main season, data lock), password changed |
| Success (green) | `#effaf3` | reward, published topic, resolved report, SMTP test |
| Neutral (grey) | `#f7f5f2` | removals, a refused topic |
| Warning (orange) | `#fff5eb` | the morning e-mail's "Traži pažnju" cards, urgent operator reports |

Table layout and inline styles (Outlook renders with Word), a dark-mode block for Apple Mail / iOS /
Outlook.com, full-width button and tighter padding under 620 px. **No web fonts** — system stacks,
Georgia for the titles. Every mail is sent with a text/plain part too.

## The model (`Common/Email/EmailContent.cs`)

`EmailContent(Title)` with `Subject`, `Eyebrow`, `Icon`, `Tone`, `Greet`, `Blocks`, `Button`,
`ShowLinkFallback` (prints a one-time link under the button), `After` (blocks under the button),
`Reason`, `LinkSettings`, `Internal` (operator mail: no help line), `Preheader`.

| Block | Draws |
|---|---|
| `EmailText` | paragraphs; "- " lines become a list — the notification format |
| `EmailFacts` | label/value rows in a soft box; `EmailFact.SentAt` prints the local send time |
| `EmailCallout` | a tinted box with an icon — "Link vrijedi 2 sata", "Podaci se ne brišu" |
| `EmailStats` | big numbers side by side |
| `EmailSection` | upper-case heading with an optional count badge |
| `EmailCard` | icon tile, title, subtitle, then text / name-detail rows / bullets, a link; `Featured` tints it |
| `EmailChecklist` | rows with an icon each, optionally linked |

Links are **paths** ("/beehives/7"), resolved against `FrontendUrl` when the worker renders; an
absolute URL (the reset/verify token links) is kept. The renderer escapes every string — builders pass
raw user text.

## Who builds what

| Mail | Builder |
|---|---|
| Potvrda e-pošte, promjena lozinke, dobrodošlice, lozinka promijenjena | `AuthEmails` |
| Jutarnji pregled | `MorningEmail.Compose` |
| Novi / dodijeljeni zadatak (task card: where, due, priority, notes) | `TodoEmails.Created` |
| Mraz (Critical), dio podataka postaje nedostupan | `AlertEmails` |
| Povratna informacija — operateru i odgovor pošiljaocu | `FeedbackEmails` |
| Tema objavljena / nije objavljena | `TopicEmails` |
| Pozivnica prihvaćena, nagrada | `InvitationEmails` |
| Prenos vlasništva | `OrgEmails` |
| Sastavljena društva | `MergeEmails` |
| Every other notification | `NotificationEmail.Compose` — the message as text |
| SMTP test | inline in `NotificationsController` |

`NotificationService.NotifyAsync(..., email: content)` mails the builder's content when the policy
says "now"; `NotificationEmail.Compose` fills what the sender left out — icon (the bell's), category,
tone (Critical priority is always red), a button from the related entity — and **always** sets the
footer: security ("stižu uvijek", no settings link), Critical ("stižu odmah"), or "Sva".

| Related entity | Button |
|---|---|
| `Beehive` / `Apiary` / `Treatment` / `Diet` | `/beehives/{id}`, `/apiaries/{id}`, `/treatments/{id}`, `/feedings/{id}` |
| `Organization` (plan notices) | `/plans` — not `/organization`, which a Beekeeper cannot open |
| `Feedback` | `/profile#povratne-informacije` |
| `LearningTopic` | `/learning/moje-teme` (the builders link the topic itself) |
| `Invitation` | `/invite` |
| `SeasonPhase` | `/learning?category=2` |
| none | `/` |

## The morning e-mail

"Dobro jutro, {ime}" · "Jutarnji pregled · ponedjeljak, 20. april" → one sentence whose verbs agree
with the counts ("čeka 1 obaveza", "čekaju 4 obaveze", "3 stvari traže pažnju") → the two numbers
(only when both are non-zero) → **Traži pažnju** (one card per alert; a grouped alert's hives as rows,
"K2 · 34 dana") → **Današnje obaveze** (checklist; `CalendarObligation.Path` links each line) → **Za
čitanje** (the phase notice, highlighted, with its Edukacija topics; the AI summary tagged "AI").
Subject: "Jutarnji pregled: 4 obaveze, 3 upozorenja", or the reading item's title when that is all
there is.

## Subjects

The content's `Subject`, else its title — no "Melarium —" prefix. Examples: "Novi zadatak za vas:
Zamijeniti okvire u K3", "❄️ Najavljen mraz — Visoko, −2 °C", "Paket ističe 30.09. — dio podataka
postaje nedostupan", "[Prijava problema · visok] Kalendar ne prikazuje tretmane".

## Adding a mail

Write a builder next to the feature (`XxxEmails.Something(...)` returning `EmailContent`), pass it as
`email:` to `NotifyAsync` — or enqueue `QueuedEmail.ForUser/ForAddress(…, content)` for mail that is not
a notification. Do not set `Reason` on a notification's content unless the reason really differs;
the policy's footer is the honest one.

## Tests

`EmailTemplateTests` (escaping, link resolution, greeting, send time, footer flags, no Google
fonts, text part), `NotificationEmailTests` (buttons per entity, tones, footers), `MorningEmailTests`
(order, rows, the phase card, Bosnian agreement), plus content assertions in the feedback, topic,
ownership, alert and agenda tests.
