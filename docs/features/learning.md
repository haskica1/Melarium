# Feature: Learning Module (Edukacija)

## Overview

An in-app knowledge section with short, seasonal, practical topics beekeepers can **read or listen
to** ("Šta raditi u julu", "Kako prepoznati varou"…). Content is **platform-wide**: authored by
SystemAdmin — or written by a user and approved by one ([SPEC-26](../specs/SPEC-26-topic-submissions.md))
— visible to all organizations once published, surfaced by relevance to the current month.
Implemented per [SPEC-06](../specs/SPEC-06-learning.md).

## Content model & domain rules

- `LearningTopic`: title (150), `LearningCategory` enum (Osnove, SezonskiRadovi, BolestiINametnici,
  Oprema, Propisi, Napredno — Bosnian labels via `BsLabels`), `Months int[]?` (Postgres `integer[]`;
  months 1–12 when the topic is seasonal, **null = evergreen**), summary (300, card teaser),
  `BodyMarkdown` (text), `IsPublished`, `PublishedAt`.
- `LearningTopicRead`: per-user read marker, **unique (TopicId, UserId)**, cascade on both FKs.
- `PublishedAt` is set on the **first** publish only — it is the guard that makes the publish
  broadcast fire exactly once (unpublish → re-publish does not re-notify).
- A draft may be saved with an empty body; **publishing requires non-empty content** (400 otherwise).
- Consumption endpoints only ever see published topics; drafts 404 for everyone outside the admin API.
- **Review fields (SPEC-26)**: `AuthorId?` (`SET NULL`), `ReviewStatus` (`None` · `Pending` ·
  `Approved` · `Rejected`), `SubmittedAt?`, `ReviewedAt?`, `ReviewedById?` (`SET NULL`),
  `RejectionReason?` (500). `None` is the default, so every pre-SPEC-26 topic is "admin-authored,
  never reviewed" without a backfill.

## API

**Consumption (`/api/learning-topics`, all authenticated roles):**

- `GET /learning-topics?category=&month=` — published only; `isRead` computed per caller via one
  grouped query (no N+1).
- `GET /learning-topics/{id}` — published only, includes `bodyMarkdown`.
- `POST /learning-topics/{id}/read` — idempotent read marker → `204`.

**Authoring (`/api/admin/learning-topics`, SystemAdmin role guard):** CRUD incl. drafts,
`PUT {id}/publish` toggle, and `POST generate-draft` (AI assist, `ai-chat` rate-limit policy).

## User submissions (SPEC-26)

Any authenticated user writes a topic and sends it for review; the SystemAdmin approves or rejects
it with a reason. **The proposal is the same row as the published article** — approval is a state
change, not a copy (ADR-044).

- **`IsPublished` remains the only visibility filter.** A proposal is created `IsPublished = false`,
  so the consumption queries cannot return it. `ReviewStatus` is in no read query; it only records
  what the row went through.
- **Submit** (`POST /learning-topics/submissions`) — `Pending`, `SubmittedAt` set, `AuthorId` = the
  caller. `learning-submit` rate limit (5/min per IP). Body is required at submit time, minimum
  **200 characters** (there is no draft state on the user side).
- **Edit** (`PUT .../{id}`) — a rejected proposal is resubmitted: back to `Pending`, the reason and
  `ReviewedAt`/`ReviewedById` cleared, admins re-notified. Editing one that is already pending is
  just an edit and notifies nobody. An **approved** topic is platform content: edit and withdraw
  both return `422`.
- **Withdraw** (`DELETE .../{id}`) — deletes an unapproved proposal.
- **Approve** (`PUT /admin/learning-topics/{id}/approve`) — sets the review fields and publishes
  through the same `MarkPublished` + `BroadcastFirstPublishAsync` helpers the publish toggle uses.
- **Reject** (`PUT /admin/learning-topics/{id}/reject`) — reason 10–500 chars, mandatory, reaches
  the author verbatim.
- Ownership reads are scoped to the caller (`GetOwnSubmissionAsync`), and someone else's id is a
  **404, not a 403** — the same rule as feedback.

### Notifications

| Moment | To | Channel | Type |
|---|---|---|---|
| Submitted / resubmitted | every SystemAdmin | in-app only (`NotifyManyInAppAsync`) | `LearningTopicSubmitted = 29` |
| Approved / rejected | the author | bell **and** email (`NotifyAsync`) | `LearningTopicReviewed = 30` |

No operator email and no new configuration (unlike SPEC-13) — a proposed topic is not an incident.
Neither channel may fail the action: the row is saved first and notification errors are logged.

## Publish notification

First publish broadcasts **one in-app notification per user, except the topic's own author**
(`LearningTopicPublished = 17` — the
spec suggested 15, but SPEC-08 shipped first and took 15/16), via
`INotificationService.NotifyManyInAppAsync` — a batch insert with a single `SaveChangesAsync` and
**deliberately no email** (an email per user per article would be spam). The author is skipped
because they received the personal `LearningTopicReviewed` message about the same topic a moment
earlier.

## AI draft assist (Phase 2)

`generate-draft { title, outline? }` → existing Groq stack via `IProseAiClient` (renamed from
`IAdvisorAiClient` when SPEC-18 retired the advisor and repurposed the client as shared free-form-prose
infrastructure — this is its other consumer) (`llama-3.3-70b-versatile`) with an authoring system
prompt: Bosnian, practical, regional context,
markdown `##` sections, no invented regulations (refer readers to the veterinary authority). The
reply carries the summary after a `---SAŽETAK---` marker; the service splits it (fallback: derive the
teaser from the body). The draft only prefills the form — **AI never publishes**. Rate limit joins
the `ai-chat` policy (SPEC-01).

## Frontend

- Nav item **"Edukacija"** (`GraduationCap`), visible to all authenticated users.
- `LearningPage` (`/learning`) — top section **"Aktuelno u {mjesecu}"** (locative month names are a
  lookup table, not string concatenation), category filter chips, topics grouped by category, cards
  with title/summary/category chip/read ✓.
- **Search by topic title** — client-side over the list the page already loads in full; no endpoint,
  no request per keystroke. Composes with the category chips (a chip narrows what is searched, and
  the empty state says so when a chip is active). While a query is present the month and category
  sections collapse into one flat **"Rezultati pretrage"** list: those sections exist for browsing,
  and splitting six hits across three headings hides how few there are.
  Matching is **diacritic-insensitive and word-order-free** via `shared/utils/search.ts` — "cistoca"
  finds "Čistoća", "matica zamjena" finds "Zamjena matice". `fold()` strips combining marks after
  NFD; **đ/đ needs its own pass** because Unicode treats it as a letter, not d + a mark.
- `LearningTopicPage` (`/learning/:id`) — `MarkdownArticle` (shared react-markdown renderer, no
  raw-HTML plugins → `<script>` renders as inert text), **listen controls** and mark-as-read.
- **Listen ("Poslušaj")** — `core/hooks/useSpeech.ts`, browser `speechSynthesis` (no backend, no
  cost): play/pause/resume/stop; voice pick `bs-*` → `hr-*` → `sr-*` → default with the hint
  "Kvalitet glasa zavisi od uređaja."; text queued as paragraph-sized utterances (long single
  utterances get cut off in some Chrome versions); **speech cancels on unmount/navigation**. The
  spoken text is the title + `stripMarkdown(body)`.
- **Mark-as-read**: fired after the topic has been open **~5 s** (timer with cleanup — a misclick
  isn't a read), then the list ✓ updates via query invalidation.
- **Proposing a topic (SPEC-26)** — "Predloži temu" and "Moje teme" in the `LearningPage` hero.
  `TopicSubmissionFormPage` (`/learning/predlozi`, `/learning/moje-teme/:id/uredi`) is the admin form
  minus the AI panel, with a 200-character counter, an explanation of what happens after sending, and
  the rejection reason shown while editing a rejected topic. `MySubmissionsPage`
  (`/learning/moje-teme`) lists the caller's proposals with status, reason, edit and withdraw.
- **Author byline** on `LearningTopicPage` when `authorName` is set; platform content has none.
- Admin authoring (`features/admin/`, under `AdminRoute`): `LearningTopicsAdminPage`
  (`/admin/learning-topics` — a "Čeka odobrenje" section with the author, *Pročitaj / Odbij /
  Odobri* and a reason dialog, then the list incl. drafts, publish toggle, delete; a rejected
  proposal shows "Odbijena" rather than "Skica") and `LearningTopicFormPage`
  (new/edit — months multi-select chips, summary counter, markdown textarea with **preview toggle**,
  AI-draft panel). Reachable via "Uredi edukaciju" on the admin dashboard hero and from the SystemAdmin nav item of the
  same name, which carries a **pending-proposal badge** (`GET /admin/learning-topics/submissions/summary`).

## Seed content

`DatabaseInitializer.SeedLearningTopicsAsync` seeds **6 starter topics in Development only** (same
policy as demo accounts; skipped when any topic exists; inserted directly so no notifications fire):
julski kalendar (7), varoa monitoring (6–8), sprječavanje rojenja (4–6), priprema za zimu (8–10),
prihrana (evergreen), higijena opreme (evergreen). Production content is entered by SystemAdmin.

## Tests

`LearningTopicServiceTests` — published list flags read topics via one grouped query; unpublished
detail → 404; mark-read idempotence (second POST is a no-op); first publish notifies every user
in-app exactly once; re-publish after unpublish does not re-notify; publish with empty body → 400;
AI draft marker parsing; AI failure → `BusinessRuleException`. **SPEC-26**: submit creates a pending
*unpublished* row and notifies the admins; a too-short body is rejected and saves nothing; a failing
admin notification does not fail the submission; someone else's proposal is a 404; resubmitting after
a rejection clears the verdict and re-notifies; editing while pending does neither; edit and withdraw
of an approved topic are refused; approve publishes, notifies the author and broadcasts to everyone
*else*; approving a non-pending topic is refused; reject keeps it unpublished and carries the reason.
