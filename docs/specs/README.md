# Feature Specs

> One spec per planned feature. Workflow: **read the spec → implement exactly what it says → tick the
> acceptance criteria → update the spec status + project docs** (per `../workflow.md`).
> A spec is the single source of truth for its feature until it ships; after shipping, the feature
> gets a `docs/features/<name>.md` file and the spec is marked ✅.

## How to implement a spec

1. Read the spec top to bottom. Read `../context.md` and `../ignore.md` first (house rule).
2. Follow the templates in `../common-patterns.md` — the spec defines *what*, the patterns define *how*.
3. Implement in the phase order given in the spec. Phases are independently shippable.
4. Tick every box in the spec's **Acceptance criteria** before considering it done.
5. Update docs per `../workflow.md` step 5 (feature file, `api-contracts.md`, `context.md`, …)
   and set the spec status to ✅ Implemented (add the date).

## Index

| # | Spec | One-liner | Effort | Depends on | Status |
|---|------|-----------|--------|------------|--------|
| 01 | [AI Advisor](SPEC-01-ai-advisor.md) | Chat savjetnik (tekst + glas) koji poznaje korisnikove košnice | M/L | — | ✅ Implemented (2026-07-03) — superseded by 18 |
| 02 | [Harvest Log](SPEC-02-harvests.md) | Evidencija vrcanja: kg meda po košnici/sezoni + profitabilnost | M | — | ✅ Implemented (2026-07-03) |
| 03 | [Queen Tracking](SPEC-03-queens.md) | Matice: starost, boja oznake, porijeklo, historija zamjena | S/M | — | ✅ Implemented (2026-07-02) |
| 04 | [Smart Alerts & Weekly Summary](SPEC-04-smart-alerts.md) | Automatska upozorenja (pregledi, med, mraz, matica) + sedmični AI sažetak | M | 03 (partly) | ✅ Implemented (2026-07-03) |
| 05 | [Inspection Photos & AI Analysis](SPEC-05-inspection-photos.md) | Fotografije na pregledima + AI analiza okvira (vision) | L | — | ✅ Implemented (2026-07-05) |
| 06 | [Learning Module](SPEC-06-learning.md) | Edukacija: sezonske teme za čitanje i slušanje | M | — | ✅ Implemented (2026-07-03) |
| 07 | [Offline Inspections](SPEC-07-offline-inspections.md) | Unos pregleda bez signala — lokalni outbox + sinhronizacija | M/L | — | ✅ Implemented (2026-07-03) |
| 08 | [Treatment Log](SPEC-08-treatments.md) | Zakonska evidencija tretmana (varoa i dr.): preparat, doza, LOT, karenca + PDF registar | M | — (02/04 soft) | ✅ Implemented (2026-07-03) |
| 09 | [Plans & Billing](SPEC-09-plans-billing.md) | Paketi i naplata: Besplatni/Standard/Pro/Max + skriveni Partner, limiti + AI gating, 30-dnevni trial, ručna godišnja aktivacija (Paddle u fazi 2) | L | — (gejtuje 01 i 10) | ✅ Implemented (2026-07-06) |
| 10 | [Apiary Migration](SPEC-10-apiary-migration.md) | Pašnjaci i selidbe: registar pašnjaka, historija selidbi, prinos po pašnjaku | M | — | ✅ Implemented (2026-07-04) |
| 11 | [Calendar Sync](SPEC-11-calendar-sync.md) | Sinhronizacija obaveza (hranjenja, todo, izvedeni rokovi) u vanjski kalendar (ICS feed univerzalno + nativni Google/MS OAuth) + dnevni podsjetnik u 8h | L | — (reuse 04/08/09) | 🔨 Faza A (2026-07-13) · B/C planirane |
| 12 | [Apiary Feeding](SPEC-12-apiary-feeding.md) | Prehrana na nivou pčelinjaka: jedan program → odabir košnica (kao tretmani), oznaka "aktivna prehrana" na košnici, ukida kopiranje | M/L | — (mijenja 01/04/11) | 📋 Planned |
| 13 | [User Feedback](SPEC-13-user-feedback.md) | Prijava problema i povratne informacije (bug/žalba/pohvala/prijedlog/pitanje) → in-app notifikacija SystemAdminima + jedan email na konfigurisanu adresu + admin dashboard za trijažu | M | — (reuse ADR-021 queue, ADR-027 storage) | ✅ Implemented (2026-07-30) |
| 14 | [In-App Help](SPEC-14-in-app-help.md) | Kontekstualna pomoć po stranici (info ikona + panel), uvodni flow za nove korisnike i izvedena "Prvi koraci" lista | M | — (soft-link 06) | ✅ Implemented (2026-07-30) |
| 15 | [Invite a Friend](SPEC-15-invite-friend.md) | "Pozovi prijatelja": lični link + email pozivnica na platformu. Pozvani dobija 60 dana trial-a umjesto 30, pozivalac +30 dana na svoj paket kad pozvani potvrdi e-poštu, uz plafon od 180 dana po organizaciji | M | — (reuse ADR-021 queue, SPEC-09 planovi) | 🔨 Faza 1 (2026-08-06) · Faza 2 (e-mail kanal) planirana |
| 16 | [Org Activity & Status](SPEC-16-org-activity-retention.md) | Da li se organizacija *koristi*: heartbeat aktivnosti + izračunat status (Aktivna / Uspavana / Za brisanje) i radne liste za naplatu u admin tabeli, ručni prekidač "Neaktivna" koji blokira prijavu, i popravljeno ručno brisanje organizacije. **Ništa se ne briše automatski** | M | — (extends 09, reuse ADR-021/027) | 🔨 Kolona "Zadnja aktivnost" isporučena (2026-08-17) **bez heartbeata** — izračun iz podataka, ADR-034; status badge, prekidač i brisanje i dalje planirani |
| 17 | [AI Asistent](SPEC-17-ai-assistant.md) | Glasovna ili tekstualna naredba → AI pokaže šta je razumio → korisnik potvrdi → radnja se izvrši. Sam pronalazi pčelinjak i košnicu, radi više radnji iz jedne rečenice, razgovorom razrješava nejasnoće, i mijenja/briše postojeće zapise uz drugu potvrdu | L | — (reuse 01 Groq stack, 09 paketi) | ✅ Implemented (2026-08-09) — Q&A extended by 18 |
| 18 | [Spajanje AI Savjetnika u AI Asistenta](SPEC-18-ai-merge.md) | Jedan AI umjesto dva: Asistent sad i odgovara na pitanja (uz kontekst košnice kad je u fokusu), ne samo izvršava naredbe. `/advisor` se gasi, stara historija se migrira, mjesečna kvota se spaja u jednu | M | 01, 17 | 🔨 U implementaciji (2026-08-09) |
| 19 | [Sastavljanje društava](SPEC-19-colony-merge.md) | Dva društva u jedno: pripojena košnica trajno izlazi iz pčelinjaka (ne briše se), prijemna nosi zapis o primljenom društvu. Bira se koja matica ostaje, zadaci/prehrana/tretmani se uredno zatvaraju, poništavanje u roku od 24h | M | — (dodiruje 03, 08, 09, 12, 17) | ✅ Implemented (2026-08-21) — migracija još nije primijenjena, vidi §12 |
| 20 | [Kontakt i podrška](SPEC-20-contact-support.md) | Kontakt modal (WhatsApp, Viber, telefon, email) dostupan sa svake stranice **i s login/register ekrana**, uz obećanje odgovora u 24h. Frontend-only, bez rute | S | — (soft-link 13) | ✅ Implemented (2026-08-28) |
| 21 | [Šta je novo](SPEC-21-announcements.md) | SystemAdmin objavi novu funkcionalnost (naslov + opis u markdownu, tip Novo/Poboljšanje/Ispravka) → banner na svakoj stranici → modal s cijelim tekstom → "x" ga trajno sklanja. Sve objave ostaju na stranici "Šta je novo". Bez stavke u zvonu, bez slike, bez ciljanja po paketu | M | — (prepisuje obrazac 06) | ✅ Implemented (2026-08-28) — migracija još nije primijenjena |
| 22 | [Moja organizacija](SPEC-22-org-profile.md) | Administrator organizacije napokon može urediti organizaciju koju je sam napravio pri registraciji: naziv, opis i logotip, na stranici "Moja organizacija". Uz to, sistemske tabele dobijaju kontakt vlasnika, potvrdu e-pošte, zadnju prijavu, filtere i sortiranje | M | — (dodiruje 05, 09, 16) | ✅ Implemented (2026-08-30) — migracija još nije primijenjena |
| 23 | [Mobilne aplikacije](SPEC-23-mobile-apps.md) | Melarium na Google Play i App Store kroz Capacitor, uz web koji ostaje netaknut: iste funkcije bez izuzetka, prave push notifikacije na telefon, i brisanje računa + prenos vlasništva organizacije koje prodavnice traže a kojih danas nema | L | — (dodiruje 04, 06, 07, 08, 09, 22) | 📋 Planned |

**Recommended order = index order.** Rationale:

- **SPEC-01 first** — the user-facing flagship; reuses the existing Groq stack (`VoiceParsingService`
  pattern, `useVoiceInput`) so infra cost is near zero. Its hive context gets richer as 02/03 ship,
  but it works without them.
- **SPEC-02/03** are pure CRUD, independent, and feed both the advisor context and the alert rules.
- **SPEC-04** builds on the notification infra and consumes data from 02/03 (queen-age rule is
  skipped gracefully until 03 ships).
- **SPEC-05** is the largest (introduces object storage) — do not start it casually.
- **SPEC-06/07** are independent and can be slotted anywhere.
- **SPEC-08** is independent CRUD like 02/03 and can be slotted anywhere (added later, not yet
  prioritized against 01–07); it feeds a harvest-form warning (02) and two alert rules (04),
  all soft dependencies in both directions.
- **SPEC-12** was added 2026-07-25. It is the only spec so far that **changes an existing shipped
  feature's data model** rather than adding one, and it runs a migration over live production data —
  so it is not a "slot it anywhere" item like 08 or 10. Its Phases A and B must deploy together.
- **SPEC-13/14** were added 2026-07-30 and are the two **user-facing product specs**, not
  infrastructure: 13 opens a feedback channel from customers back to SystemAdmin, 14 explains the app
  to new users. Both are additive and low-risk (13 = one new table, no existing consumer touched; 14 =
  frontend-only, no schema at all), so unlike SPEC-12 they can ship independently and in either order.
  14's bulk cost is **writing Bosnian help copy**, not code.
- **SPEC-15** was written 2026-08-06, after an earlier draft of the same feature was scrapped for
  over-specifying. Its product decisions were settled one at a time before drafting and are listed in
  §1; the document is the record, not the place they were made. Two things in it must be implemented
  literally rather than approximately, because both are silent failures: the reward **never writes to
  `PlanNotes`** (`PlansPage` detects the trial by exact string match, so a tidy audit line would erase
  the trial notice), and the grant runs **after** the verification is committed (a shared `DbContext`
  means `try/catch` alone does not stop a failed reward from taking down the verification). Its two
  phases are independently shippable and go in order: the share link and the reward first, the email
  channel second — deliberately, so the code that grants plan value reaches production before the code
  that mails arbitrary addresses.
- **SPEC-16** was added 2026-07-31. Its defining decision is what it **does not** do: the system measures
  usage and *labels* an organization, but **never deletes anything on its own** (§0 D2) — every deletion is
  a SystemAdmin clicking through a confirm-by-name modal. Read that constraint before implementing, because
  the obvious "helpful" additions (a countdown, a warning e-mail, a nightly cleanup job) were considered and
  rejected, not overlooked. Two parts of it change behaviour rather than adding to it, and both are
  deliberate: a deactivated organization's users **cannot sign in** (Phase B), and `DeleteOrganizationAsync`
  stops refusing organizations that have users (Phase C). Phase C also fixes a **pre-existing bug it did not
  introduce** — `DeleteUserAsync` throws on an FK when the user has an assigned todo.
- **SPEC-17** was written 2026-08-08 and shipped complete (all three phases) 2026-08-09. It is the first
  feature in which **an AI writes to the database**, so the whole spec is arranged around one rule that must
  not be "optimized" later (§5.1): the executor builds the same DTOs the forms post and calls the **existing**
  services — `InspectionService.CreateAsync`, `TodoService.CreateAsync`, and by Phase C their `Update`/`Delete`
  counterparts too — never repositories. Going around them silently drops `IAccessGuard`, the plan limits, the
  automatic weather temperature and the todo notification cascade, with no compile-time error to catch it. Two
  consequences of that rule are easy to miss and are called out where they bite: validation in this codebase
  lives in the **controllers**, so the executor must run the validators itself or AI-authored data passes
  fewer checks than typed data (§5.2); and the confirm request is **untrusted input** like any form post,
  because the card is editable and the client can send any hive id (§5.3). Its three phases shipped
  independently and in order — creation, then the conversational follow-up, then update/delete — deliberately,
  so the only path that can overwrite or destroy a correct record reached production **after** the prompt and
  the resolver had met real usage in the first two phases. Phase C also narrowed D5 rather than applying it
  literally: `CompleteTodo` turned out not to need the second confirmation, because checking a todo off is a
  one-tap, instantly-reversible toggle everywhere else in the app already — treating it as destructive would
  have invented risk the rest of the UI does not recognize for the identical action. It does **not** fix the
  pre-existing `DateTime.UtcNow`-as-local bug in `VoiceParsingService` (§3.1): new code uses `AppTimeZone`, the
  old bug is tracked separately.
- **SPEC-18** was written and started 2026-08-09, the day after SPEC-17 shipped. It answers a question
  SPEC-17 §D2 had itself already asked and rejected — merging the advisor into the assistant — and explains
  why the two objections that killed it then (a router guessing "question or command", a chat bubble hosting
  an editable form) no longer apply now that Phases A/B/C actually exist: the envelope's `actions` array was
  always allowed to be empty, so "this was a question" needs no separate router step, and proposal cards were
  never rendered inside the reply bubble to begin with. It is deliberately **additive** to SPEC-17's own
  behaviour (Phases A/B/C are untouched) plus a retirement of SPEC-01's surface. Two decisions were made
  explicitly rather than inferred: old advisor conversations are **migrated**, not archived or discarded, into
  the unified history (`deploy/data-migration/advisor-merge/`, run by hand against production, same rigor as
  the July 2026 old-database import); and the two previously-separate monthly quotas become **one** combined
  counter, because the quota gate necessarily runs *before* the model call that would reveal whether a turn
  was a question or a command — a single pre-flight number is not just simpler, it is what that ordering
  requires.
- **SPEC-19** was written 2026-08-21 from Asim's idea, with its product decisions settled one at a time
  before drafting and listed in §1 — the document is the record, not the place they were made. It is the
  first spec whose point is to make an existing row **stop being listed** rather than to add a new one, and
  that is where its whole risk sits: §5 enumerates **thirteen** read sites that must learn the filter, and
  states why the one-line alternative (an EF global query filter) was rejected — `TreatmentEntry.Beehive`
  is a required navigation, so EF would silently null it and the legally-retained treatment register would
  print without hive names. That register is also why this feature exists at all: the only way to remove a
  hive today, `DELETE /api/beehives/{id}`, cascades into `TreatmentEntry` and destroys a 5-year record.
  Two decisions must be implemented literally rather than approximately, because both are silent
  falsifications otherwise. The queen order in §3.2 (`KeptSource` closes the *target's* queen **before**
  moving the source's) — reversed, it trips the existing "already has an active queen" rule. And feeding is
  removed **per hive** (`DietService.RemoveBeehiveAsync`), never by stopping the programme: since SPEC-12 a
  diet is an apiary-level programme, so "close the feeding" for one hive would end it for every other hive
  on it. Its Phase C (the AI action) goes last deliberately, so the only path that lets a model start an
  irreversible change reaches production after the service has met real usage — the same ordering SPEC-17
  used for its own update/delete phase.
- **SPEC-20** was written 2026-08-28 from Asim's idea, its product decisions settled one at a time
  before drafting and listed in its Domain rules — the document is the record, not the place they were
  made. It is the smallest spec in the set and the only one that is **pure UI with no data at all**:
  no entity, no endpoint, no migration, no env var. Its one decision worth reading before changing
  anything is that contact is a **modal and not a route** (D1): a `/kontakt` route would unmount the
  login form and discard the typed credentials, and the user who cannot sign in is exactly the one
  reaching for it — so the missing shareable URL is a price that was paid knowingly, not an oversight.
  Two smaller rules are there for the same reason and are easy to "tidy away": the contact details are
  a **frontend constant** rather than a fetch, because the app is used offline in the field and a
  fetched number would be missing exactly when the phone is the only channel left (D2); and every row
  has a **copy button with a select-the-text fallback**, because a `viber://` link on a desktop
  without Viber installed does nothing at all and swallowing a clipboard rejection would reproduce the
  very silent failure the button exists to prevent (D4). The contact address is `info@melarium.app`;
  `noreply@melarium.app` stays what it is, the Resend sending identity.
- **SPEC-21** was written 2026-08-28 from Asim's idea, its product decisions settled one at a time
  before drafting and listed in its Domain rules — the document is the record, not the place they were
  made. It copies SPEC-06's shape almost line for line (SystemAdmin authors, platform-wide content,
  a per-user read marker instead of a notification fan-out), which is why the effort is M and not L;
  the table is nonetheless **separate** on purpose, because Edukacija is beekeeping knowledge and this
  is news about the app, and merging them would pollute `/learning` with release notes. Its one rule
  worth reading before changing anything is D1: the banner shows **only the latest** announcement,
  never a queue. The obvious "improvement" — walking backwards through everything the user has not
  seen — was considered and rejected, because a user returning after three publishes then gets three
  banners in a row and a newly-registered user gets a wall of them. The accepted cost is that an
  announcement can be skipped in the banner entirely, and D8's unread badge on the menu item exists
  precisely to catch it; removing that badge as redundant would leave the gap open. Two smaller rules
  are deliberate and easy to "tidy" in the wrong direction: there is **one** seen-state, not a separate
  read and dismissed pair (D2 — closing the modal dismisses, since a user who read the whole text needs
  no further banner), and publishing writes **nothing** to `Notification` (D4), so that dismissing the
  banner cannot leave an unread bell item behind for the same announcement.
- **SPEC-22** was written 2026-08-30 **after** the feature was built, from four decisions Asim made
  up front (§ Decisions) — the same working order as 20 and 21: his idea, the choices settled one at a
  time, the document written last. The decision that shapes everything else is D1: contact and official
  fields (e-pošta, telefon, adresa, JIB, broj registra pčelara) were offered, argued for, and
  **declined** for v1 — "samo osnovna polja, za sad". So the migration adds two nullable columns for
  the logo and nothing else; anyone re-reading this and thinking the org model looks thin should add
  those fields when they are actually needed, not because they were once proposed. D4 is the one that
  must not be "simplified": every endpoint in the slice resolves the organization from
  `ICurrentUser.OrganizationId` and there is **no id in any route** — adding `/organizations/{id}`
  "for symmetry with /admin" would turn a structurally tenant-safe slice into one that depends on
  remembering an access check. D6/ADR-040 explains why "zadnja prijava" is a query over refresh tokens
  and not a `User.LastLoginAt` column, and D7 why the "Vlasnik" column is the org's OrganizationAdmin
  rather than `CreatedBy` (for an org the SystemAdmin created, `CreatedById` is Asim).
- **SPEC-23** was written 2026-08-31 from Asim's idea — users kept asking how to *download* the app —
  with its product decisions settled one at a time before drafting and listed in § Decisions; the
  document is the record, not the place they were made. It is the first spec whose deliverable is not
  a screen but a **distribution channel**, and that is where its shape comes from: the web app is
  explicitly untouched (D1 wraps the existing `vite build` output rather than rewriting it), so almost
  nothing in the spec is a new feature. The bulk of it is the opposite — § Domain rules enumerates six
  things that **already work on the web and would silently stop working** inside a webview, because
  the one hard rule Asim set is that the mobile app may not have a single feature fewer than the web.
  Two of those are invisible failures rather than errors and must not be "noticed later": jsPDF
  `.save()` is a browser download and does nothing at all in a webview (the treatment register and the
  QR sheets), and `useSpeech`'s `isSupported` guard would quietly *hide* the "Poslušaj" button on
  Android rather than fail, which reads as a missing feature and not as a bug. D4 is the single
  deliberate web/mobile difference in the whole spec — the upgrade CTA is hidden in the app (Apple
  3.1.1) — and it is written down precisely so that any *other* difference someone finds later is
  treated as a defect. Its riskiest part is not the native shell but § Brisanje računa, which the
  stores require and which does not exist anywhere in the code today: step 3 (`Todo.AssignedToId = null`)
  is not optional bookkeeping but the fix for a **pre-existing** FK crash that `AdminService.DeleteUserAsync`
  still has, since `TodoConfiguration` binds that FK with `DeleteBehavior.NoAction`. D6's three-case
  table is the other thing that must be implemented literally: the obvious simplification — "the org
  admin leaves, so delete the organization" — would let one person destroy five other beekeepers'
  hives, records and logins without their consent, which is why D7 (transfer of ownership) exists at
  all. Deleting a solo admin's organization *does* destroy the legally-retained treatment register
  that SPEC-19 was written to protect; that is correct here and is why the confirmation must say so
  out loud. Finally, D5 has no code in it and is still the longest pole in the project: personal store
  accounts mean Google requires ~12 testers running the app for 14 continuous days, so Phase 0 starts
  the day Phase 1 does, not when the code is finished.
- **SPEC-09/10** were added 2026-07-03 and are **not yet prioritized** (against 05, the last
  remaining roadmap item). 09 changes the business model — implement deliberately, not casually;
  its v1 is manual billing (Stripe unavailable in BiH; Paddle in Phase 2). 10 is independent CRUD
  whose one design pivot is the coordinate snapshot on move (keeps weather/alerts/map untouched).

## Conventions used in the specs

- Layer layout, DTO/validator/service/controller templates: `../common-patterns.md`.
  Application code lives in `Melarium.Application/Features/<Feature>/`, repositories + migrations in
  `Melarium.Entity/` (persistence project), matching current code — not the older paths some docs mention.
- Authorization is **always** via `IAccessGuard` (never inline role checks in services/controllers).
- All user-facing strings (UI, notifications, AI output) are **Bosnian**; enum display labels via `BsLabels`.
- New secrets go to env vars / user-secrets, never the repo (`appsettings.json` keeps empty placeholders).
- Every schema change = one EF Core migration in `Melarium.Entity/Migrations/`, named `Add<Thing>`.
- Statuses: 📋 Planned · 🔨 In progress · ✅ Implemented (date).
