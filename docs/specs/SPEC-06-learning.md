# SPEC-06 — Learning Module ("Edukacija")

| | |
|---|---|
| **Status** | ✅ Implemented (2026-07-03) |
| **Effort** | M (~2 days) |
| **Depends on** | nothing (Phase 2 AI drafting reuses `Groq:ApiKey`) |
| **New packages** | `react-markdown` (frontend) — **flagged per house rule, approve before implementing** |

## Goal

An in-app knowledge section with short, seasonal, practical topics beekeepers can **read or
listen to**: "Šta raditi u julu", "Kako prepoznati varou", "Priprema za zimu". Content is
platform-wide (written once by SystemAdmin, visible to all organizations), surfaced by relevance
to the current month. Keeps users opening the app between inspections.

## Content model

```
LearningTopic : BaseEntity
  Title         string(150)
  Category      LearningCategory enum { Osnove=1, SezonskiRadovi=2, BolestiINametnici=3,
                                        Oprema=4, Propisi=5, Napredno=99 }
  Months        int[]?          // Postgres int[]; months (1–12) when topic is "aktuelno"; null = evergreen
  Summary       string(300)     // card teaser
  BodyMarkdown  text            // the article
  IsPublished   bool
  PublishedAt   DateTime?

LearningTopicRead : BaseEntity          // read tracking
  TopicId  int (FK cascade), UserId int (FK cascade)
  → unique index (TopicId, UserId)
```

`ILearningTopicRepository` + `IUnitOfWork`. Migration `AddLearningTopics`.

## Backend endpoints

**Consumption (all authenticated roles):**

| Method | Path | Notes |
|---|---|---|
| GET | `/api/learning-topics?category=&month=` | published only → `LearningTopicSummaryDto[]` (+ `isRead` per current user, computed via one grouped query — no N+1) |
| GET | `/api/learning-topics/{id}` | published only → detail with `bodyMarkdown` |
| POST | `/api/learning-topics/{id}/read` | idempotent; marks read for current user → `204` |

**Authoring (SystemAdmin only, `/api/admin/learning-topics`):** standard CRUD incl. unpublished
listing and a publish toggle. On **first** publish: create in-app notifications (type
`LearningTopicPublished = 15` — coordinate the enum number with SPEC-04 if it ships first) for all
active users, **in-app only, no email** (batch insert; email digest would be spam).

Validation: title/summary lengths; `months` values 1–12; body non-empty for publish.

## Frontend

- `features/learning/`:
  - `LearningPage` (`/learning`) — top section **"Aktuelno u {mjesec}"** (topics whose `Months`
    contains the current month), then all topics grouped/filterable by category; cards show title,
    summary, category chip, read ✓.
  - `LearningTopicPage` (`/learning/:id`) — rendered markdown (`react-markdown`, no raw-HTML
    plugins — default escaping is the XSS guard), category/months chips, **listen controls**.
  - Admin authoring UI: `features/admin/` — list + form (title, category, months multi-select,
    summary, markdown textarea with a simple preview toggle, publish switch). Guarded by the
    existing `AdminRoute`.
- **Listen ("Poslušaj")** — browser `speechSynthesis`, no backend and no cost:
  - Play/pause/stop; reads title + body (markdown stripped to plain text).
  - Voice pick order: `bs-*` → `hr-*` → `sr-*` → default; if none found, show the control anyway
    (default voice) + hint "Kvalitet glasa zavisi od uređaja."
  - Stop speech on unmount/navigation (cleanup in effect).
- Mark-as-read: fire `POST /read` after the topic has been open ~5 s (timer, not on mount — a
  misclick isn't "read").
- Sidebar item "Edukacija" (`GraduationCap` icon). Routes in `App.tsx` (admin route under `AdminRoute`).

## Phase 2 — AI draft assist (authoring only)

`POST /api/admin/learning-topics/generate-draft` `{ title, outline? }` → Groq
`llama-3.3-70b-versatile` (temp 0.5, max_tokens 2000) → `{ bodyMarkdown, summary }` prefilled into
the form for the admin to **edit and publish manually**. AI never publishes. Prompt: Bosnian,
practical, regional beekeeping context, markdown with `##` sections, no invented regulations —
where propisi are relevant, advise checking local veterinary authority. Rate limit: joins `ai-chat`
policy (SPEC-01) or its own 5/min if SPEC-01 hasn't shipped.

## Seed content

Seed **6 starter topics in Development only** (same policy as demo accounts). Production content
is entered by SystemAdmin. Suggested starters: Kalendar radova za tekući mjesec; Prepoznavanje i
monitoring varoe; Sprječavanje rojenja; Priprema zajednica za zimu; Prihrana — kada i čime;
Higijena i dezinfekcija opreme.

## Out of scope (v1)

Quizzes/certificates; comments; favorites; video; per-organization private content; multi-language;
pre-recorded audio files; push notifications.

## Acceptance criteria

- [x] Topic for month 7 appears in "Aktuelno u julu" in July and not in the section in August
      (unit-test the query filter; UI spot-check). *(Filter forwarding unit-tested at the service
      boundary — the tests project has no EF provider; the month predicate itself is the one-line
      `Months.Contains` in `LearningTopicRepository`.)*
- [x] Unpublished topics: invisible on `/learning` and its API for non-SystemAdmin (guard test),
      visible in admin list.
- [x] Read tracking: ✓ appears after reading, survives reload, idempotent double-POST.
- [x] Listen: starts/stops correctly, stops on navigation, doesn't crash when no BCS voice exists.
- [x] Markdown renders headings/lists/links; `<script>` in body renders inert as text.
- [x] Publish fires one in-app notification per active user, exactly once (re-publish toggle does
      not re-notify).
- [x] Docs updated: `features/learning.md`, `api-contracts.md`, `context.md` (+ package decision in
      `decisions.md` ADR-025), this spec → ✅. `LearningTopicPublished` landed as enum **17**
      (spec said 15, but SPEC-08 shipped first and took 15/16).
