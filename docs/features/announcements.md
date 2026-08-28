# Šta je novo (SPEC-21)

> Implemented 2026-08-28. Spec: [`specs/SPEC-21-announcements.md`](../specs/SPEC-21-announcements.md).

## What it is

The channel the platform operator uses to tell users a feature exists. SystemAdmin writes an
announcement (title + markdown body + type), publishes it, and every user gets a banner at the top
of every page. Clicking it opens the full text in a modal; the "x" hides it for good. Everything
ever published — dismissed or not — stays on the **Šta je novo** page at `/announcements`.

It copies SPEC-06 (Edukacija) almost line for line, but is a **separate table**: Edukacija is
beekeeping knowledge, this is news about the app.

## Data

| Table | Holds |
|---|---|
| `Announcements` | `Title` (≤150), `BodyMarkdown`, `Type`, `IsPublished`, `PublishedAt` |
| `AnnouncementReads` | One row per (announcement, user) — **unique**, written only when a user acts |

`AnnouncementType`: `1 New` (Novo) · `2 Improvement` (Poboljšanje) · `3 Fix` (Ispravka). Bosnian
labels come from `BsLabels.Label(AnnouncementType)` on the backend and `AnnouncementTypeLabels` on
the frontend, matching how `LearningCategory` is handled.

Migration: `20260828132327_AddAnnouncements`. Two new tables; nothing existing is touched.

## Files

| File | Role |
|---|---|
| `Domain/Entities/Announcement.cs`, `AnnouncementRead.cs` | Entities |
| `Domain/Enums/AnnouncementType.cs` | The three types |
| `Application/Features/Announcements/` | `AnnouncementService` + DTOs + validator |
| `Entity/Repositories/AnnouncementRepository.cs` | Queries (all translate — verified via `ToQueryString`) |
| `API/Controllers/AnnouncementsController.cs` | Consumption, `[Authorize]` |
| `API/Controllers/Admin/AnnouncementsAdminController.cs` | Authoring, `[Authorize(Roles = SystemAdmin)]` |
| `shared/components/AnnouncementBanner.tsx` | The banner; owns the modal state |
| `shared/components/AnnouncementModal.tsx` | Full text (`Modal` + `MarkdownMessage`) |
| `shared/components/announcementType.ts` | Badge colours, shared by banner/modal/page |
| `features/announcements/AnnouncementsPage.tsx` | `/announcements` — the archive + type filter |
| `features/admin/AnnouncementsAdminPage.tsx`, `AnnouncementFormPage.tsx` | Authoring UI |

`Layout` renders the banner inside `<main>` but **outside** `ErrorBoundary`, so a page that crashes
does not take the announcement down with it.

## Six things that are deliberate

**The banner shows only the latest announcement — never a queue.** Not "the newest unseen", which
would walk backwards through everything missed: a user returning after three publishes would get
three banners in a row, and a new user a wall of them. The accepted cost is that an announcement can
be skipped in the banner entirely if two are published between one user's logins. The **unread badge
on the menu item** exists to catch exactly that, and the archive page holds everything — do not
remove the badge as redundant.

**One seen-state, not two.** `AnnouncementRead` is written both by the banner's "x" and by closing
the modal. A user who read the whole text needs no further banner, so a separate "dismissed" flag
would only create a way for the two to disagree.

**Seen-state lives in the database, not `localStorage`.** Unlike `helpStorage.ts`: a browser-local
flag would mean dismissing on the phone and being met by the same banner on the laptop.

**Publishing writes nothing to `Notification`.** Edukacija inserts one row per user on first
publish; this does not. Otherwise dismissing the banner would leave an unread bell item for the same
announcement — and the marker model only writes a row when a user actually acts. The bell stays for
what happened to a user's *hives*; the banner is for what changed in the *app*.

**No targeting.** Every authenticated user sees every published announcement regardless of plan or
role, SystemAdmin included — that is also the only way to check how an announcement looks.

**Ordering is by `PublishedAt`, not `CreatedAt`.** A draft written in January and published in March
is a March announcement.

## Editing after publishing

`PublishedAt` is stamped on the **first** publish only, and `PUT /admin/announcements/{id}` never
touches `AnnouncementRead` rows — so fixing a typo does not put the banner back in front of everyone
who already dismissed it. Unpublishing keeps `PublishedAt`, so re-publishing returns the announcement
to its place in the chronology instead of jumping it to the top.

## Content shape

Title + markdown body. **No image and no CTA link** — both were considered and left out of v1 (an
image is the first candidate if plain text turns out not to land).

**The banner shows type, title and "Pročitaj više" — no body text at all.** A one-line teaser
derived from the body was built first and then removed: it made the banner taller while still not
saying enough to let anyone skip opening the modal. The content lives in one place, the modal.

## Not included

Image/screenshot, "open the feature" deep-link button, targeting by plan or role, e-mail on publish,
AI draft assist (Edukacija has one), scheduled publishing, a release-version field.
