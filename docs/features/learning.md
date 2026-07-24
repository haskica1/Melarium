# Feature: Learning Module (Edukacija)

## Overview

An in-app knowledge section with short, seasonal, practical topics beekeepers can **read or listen
to** ("Šta raditi u julu", "Kako prepoznati varou"…). Content is **platform-wide**: authored by
SystemAdmin, visible to all organizations once published, surfaced by relevance to the current month.
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

## API

**Consumption (`/api/learning-topics`, all authenticated roles):**

- `GET /learning-topics?category=&month=` — published only; `isRead` computed per caller via one
  grouped query (no N+1).
- `GET /learning-topics/{id}` — published only, includes `bodyMarkdown`.
- `POST /learning-topics/{id}/read` — idempotent read marker → `204`.

**Authoring (`/api/admin/learning-topics`, SystemAdmin role guard):** CRUD incl. drafts,
`PUT {id}/publish` toggle, and `POST generate-draft` (AI assist, `ai-chat` rate-limit policy).

## Publish notification

First publish broadcasts **one in-app notification per user** (`LearningTopicPublished = 17` — the
spec suggested 15, but SPEC-08 shipped first and took 15/16), via
`INotificationService.NotifyManyInAppAsync` — a batch insert with a single `SaveChangesAsync` and
**deliberately no email** (an email per user per article would be spam).

## AI draft assist (Phase 2)

`generate-draft { title, outline? }` → existing Groq stack via `IAdvisorAiClient`
(`llama-3.3-70b-versatile`) with an authoring system prompt: Bosnian, practical, regional context,
markdown `##` sections, no invented regulations (refer readers to the veterinary authority). The
reply carries the summary after a `---SAŽETAK---` marker; the service splits it (fallback: derive the
teaser from the body). The draft only prefills the form — **AI never publishes**. Rate limit joins
the `ai-chat` policy (SPEC-01).

## Frontend

- Nav item **"Edukacija"** (`GraduationCap`), visible to all authenticated users.
- `LearningPage` (`/learning`) — top section **"Aktuelno u {mjesecu}"** (locative month names are a
  lookup table, not string concatenation), category filter chips, topics grouped by category, cards
  with title/summary/category chip/read ✓.
- `LearningTopicPage` (`/learning/:id`) — `MarkdownArticle` (shared react-markdown renderer, no
  raw-HTML plugins → `<script>` renders as inert text), **listen controls** and mark-as-read.
- **Listen ("Poslušaj")** — `core/hooks/useSpeech.ts`, browser `speechSynthesis` (no backend, no
  cost): play/pause/resume/stop; voice pick `bs-*` → `hr-*` → `sr-*` → default with the hint
  "Kvalitet glasa zavisi od uređaja."; text queued as paragraph-sized utterances (long single
  utterances get cut off in some Chrome versions); **speech cancels on unmount/navigation**. The
  spoken text is the title + `stripMarkdown(body)`.
- **Mark-as-read**: fired after the topic has been open **~5 s** (timer with cleanup — a misclick
  isn't a read), then the list ✓ updates via query invalidation.
- Admin authoring (`features/admin/`, under `AdminRoute`): `LearningTopicsAdminPage`
  (`/admin/learning-topics` — list incl. drafts, publish toggle, delete) and `LearningTopicFormPage`
  (new/edit — months multi-select chips, summary counter, markdown textarea with **preview toggle**,
  AI-draft panel). Reachable via "Uredi edukaciju" on the admin dashboard hero.

## Seed content

`DatabaseInitializer.SeedLearningTopicsAsync` seeds **6 starter topics in Development only** (same
policy as demo accounts; skipped when any topic exists; inserted directly so no notifications fire):
julski kalendar (7), varoa monitoring (6–8), sprječavanje rojenja (4–6), priprema za zimu (8–10),
prihrana (evergreen), higijena opreme (evergreen). Production content is entered by SystemAdmin.

## Tests

`LearningTopicServiceTests` — published list flags read topics via one grouped query; unpublished
detail → 404; mark-read idempotence (second POST is a no-op); first publish notifies every user
in-app exactly once; re-publish after unpublish does not re-notify; publish with empty body → 400;
AI draft marker parsing; AI failure → `BusinessRuleException`.
