# SPEC-14 — Pomoć u aplikaciji i uvod za nove korisnike ("In-app help & onboarding")

| | |
|---|---|
| **Status** | ✅ Implemented (2026-07-30) — see `features/help-onboarding.md` + ADR-031 |
| **Effort** | M (~1–1.5 days; frontend-only in Phase A/B — most of the time is *writing the Bosnian help text*, not code) |
| **Depends on** | nothing new. Soft-links to SPEC-06 (Edukacija) for long-form content |
| **New secrets / packages** | none |
| **Breaking** | No — purely additive |

## Goal

A new user lands on Melarium and gets an empty apiary list with no explanation of what an "pčelinjak"
record is for, what to do first, or what half the pages even do. Today the only help that exists is
Edukacija (SPEC-06) — long-form beekeeping articles, which answer *"kako se pčelari"*, not
*"kako radi ova stranica"*.

This spec adds **contextual, per-page help**: an info icon that is in the same place on every page and
opens a panel explaining what the page is for, how to do the main thing on it, and what to watch out
for. On top of that, two things that make it genuinely useful rather than decorative: a short
**welcome flow** on first sign-in, and a **"Prvi koraci" checklist** that tracks real progress through
the first three things a new beekeeper needs to do.

## User stories

- As a brand-new user signing in for the first time, I get a short welcome that tells me what Melarium
  does and what my first three steps are — not a blank screen.
- As any user on any page, I see an info icon in the same spot and can read what this page is for,
  whenever I want, however many times I want.
- As a new user opening a page for the first time, the help opens for me automatically, and I can say
  "ne prikazuj više" once and never be nagged again.
- As a user stuck on an empty page ("Još nema košnica"), the empty state itself offers me the help for
  that page — because that's exactly the moment I don't know what to do.
- As a Beekeeper (limited role), the help tells me what *I* can do on this page, not what an admin can.
- As a user who wants to go deeper than "which button does what", the help panel points me to the
  matching Edukacija category.

## Domain rules

### The model decision (read this first)

**Help content lives in the frontend code as a typed registry — not in the database.** This is the
central decision of the spec and the reason it is frontend-only.

| | Static registry in code (chosen) | DB table + SystemAdmin editor |
|---|---|---|
| Effort to ship | one file of content, no backend | entity, migration, repo, service, 2 controllers, admin CRUD UI |
| Editing without a deploy | no | yes |
| Stays in sync when a page changes | **yes — same commit as the UI** | no, silently rots |
| Works offline (PWA, field use) | **yes, it's in the bundle** | no, needs a request |
| Plan/auth gating questions | none | who may read/edit, does it count against a plan… |

The decisive argument is the third row. Help text that describes a UI *is documentation of code*: when
`TreatmentFormPage` changes, its help text has to change in the same commit or it starts lying to
users. A database copy has no mechanism to notice that the UI moved. The second row is the only real
loss, and it is small here — Asim is the person who both writes the help text and runs the deploy.

The app already has the right home for the other kind of content: **Edukacija (SPEC-06) stays the
DB-backed CMS for long-form, admin-authored beekeeping knowledge.** So the division of labour is:

> **Short, UI-coupled "kako radi ova stranica" → static registry (this spec).
> Long-form, seasonal "kako se pčelari" → Learning Topics (SPEC-06).**

The help panel therefore *links to* the relevant Edukacija category rather than duplicating it.

### The second decision: one icon in `Layout`, not thirty icons in thirty pages

The help icon is rendered **once**, in the shared header in `Layout.tsx`, and resolves its content from
the current route. It is **not** added page by page.

- ~30 routes would otherwise each need a manual edit, and any page whose author forgot would silently
  have no help — the inconsistency the feature exists to remove.
- The icon is then guaranteed to be in the same position on every page, which is what makes it
  learnable.
- A page with no registry entry renders **no icon at all** (not a disabled or empty one) — so a missing
  entry degrades to today's behaviour instead of an empty popup.

Route → content resolution uses `matchPath` from `react-router-dom` (already a dependency), matching
**most-specific-first** so `/apiaries/new` wins over `/apiaries/:id` — the same ordering concern
`App.tsx` already handles explicitly for its routes.

### The third decision: onboarding progress is derived, never stored

The "Prvi koraci" checklist state is **computed from data the app already fetches** — does the user
have ≥1 apiary, ≥1 beehive, ≥1 inspection — not from a persisted `onboardingCompleted` flag.

This follows ADR-028's reasoning for the computed effective plan verbatim: deriving state "avoids a
background job and a whole class of 'we forgot to flip the flag' bugs". It also behaves correctly in
cases a flag gets wrong — a user added to an organization that already has apiaries doesn't get shown
"napravi svoj prvi pčelinjak", and a user who deletes everything gets the guidance back.

Consequence: **no new table, no migration, no endpoint for the checklist.** It reads the existing
apiary/beehive queries.

### Rules

- Help is available to **every** authenticated role. It is not a paid feature and is not plan-gated —
  gating the explanation of a page behind a plan would be actively hostile.
- Content may carry **role-specific notes** (`notesByRole`), because the same page genuinely means
  different things per role — a Beekeeper on `/treatments` is read-only, an OrgAdmin is not. The base
  content is shared; only the extra note differs.
- **Auto-open on first visit** happens at most once per (user, help key). Dismissal is remembered in
  `localStorage`, keyed by the user's email — the same email-keying precedent the offline outbox uses
  (`core/offline/outbox.ts`), since the client session has no numeric user id.
- Auto-open is suppressed entirely when the user has ticked **"Ne prikazuj automatski pomoć"** (one
  global flag, offered inside the panel and on `ProfilePage`), and never fires on a form page the user
  arrived at mid-flow — a modal appearing over a half-filled form is a bug, not help.
- The panel is **always** manually reachable regardless of any dismissal. Dismissal only ever stops
  *automatic* opening.
- Help content is **Bosnian only**, consistent with ADR-023.
- The registry must contain **no secrets, no internal URLs, and no role/permission claims that aren't
  actually enforced** — it is user-facing product copy, not documentation of the security model.

## Frontend

### New: the help content registry

`frontend/src/core/help/helpContent.ts` — a typed record, one entry per page (or per page-group where
a list and its form share an explanation):

```ts
export interface HelpFaqItem { q: string; a: string }

export interface HelpEntry {
  /** Route pattern this entry covers, e.g. '/beehives/:id'. Matched via matchPath. */
  route: string
  title: string                       // "Košnica"
  summary: string                     // 1–2 sentences: what this page is for
  steps?: string[]                    // "Kako uraditi glavnu stvar" — numbered in the UI
  tips?: string[]                     // what matters / common mistakes
  faq?: HelpFaqItem[]
  notesByRole?: Partial<Record<UserRole, string>>
  /** Deep-links to /learning?category=… — never a topic id (ids differ per environment). */
  learningCategory?: LearningCategory
}
```

**Linking to Edukacija by *category*, never by topic id**, is deliberate: learning-topic ids are
database-generated and differ between environments, so a hardcoded `/learning/7` would point at the
wrong article (or a 404) outside whatever environment it was written in. The category filter already
exists on `LearningPage`.

### Coverage — every authenticated page gets an entry

Derived from the actual route table in `App.tsx`. **Priority column: P1 must ship in Phase A, P2 in
Phase B.** Nothing is left without an entry by the end of Phase B.

| Help key (route) | Page | Priority |
|---|---|---|
| `/apiaries` | Lista pčelinjaka — the landing page for most users | **P1** |
| `/apiaries/new`, `/apiaries/:id/edit` | Forma pčelinjaka (incl. map picker) | **P1** |
| `/apiaries/:id` | Detalji pčelinjaka (hives, weather, todos, moves, treatments) | **P1** |
| `/beehives/new`, `/beehives/:id/edit` | Forma košnice (type/material, auto QR) | **P1** |
| `/beehives/:id` | Detalji košnice (inspections, queen, feeding, yield, QR) | **P1** |
| `/inspections/new`, `/inspections/:id/edit` | Forma pregleda (voice input, photos, offline) | **P1** |
| `/calendar` | Kalendar obaveza | P2 |
| `/calendar/settings` | Sinhronizacija kalendara (ICS / Google / MS) | P2 |
| `/harvests`, `/harvests/new` | Vrcanja | P2 |
| `/treatments`, `/treatments/new` | Tretmani — legal register, karenca, PDF | **P1** (legal weight) |
| `/feedings/:id`, `/feedings/new` | Prehrana | P2 |
| `/advisor` | AI Savjetnik — what it knows, what it can't | **P1** |
| `/learning`, `/learning/:id` | Edukacija (incl. "Poslušaj" TTS) | P2 |
| `/stats` | Statistika | P2 |
| `/expenses`, `/expenses/new`, `/expenses/scan` | Troškovi + skeniranje računa (OCR) | P2 |
| `/pastures` | Pašnjaci i selidbe | P2 |
| `/members`, `/members/:id/assignments` | Članovi i dodjela košnica | **P1** (role model is the most-asked question) |
| `/outbox` | Neposlani pregledi (offline) | **P1** (users hit this confused, not on purpose) |
| `/plans` | Paketi i pretplata | P2 |
| `/profile` | Moj profil | P2 |
| `/admin`, `/admin/learning-topics` | SystemAdmin screens | P2 (audience of one) |

Explicitly **no** help entry for: `/login`, `/register`, `/forgot-password`, `/reset-password`,
`/verify-email`, `/scan/:uniqueId` — all are outside `Layout` (so there is no header to host the icon)
and are single-purpose screens.

### New components

- **`HelpButton`** (`shared/components/HelpButton.tsx`) — the `HelpCircle` lucide icon in the header.
  Renders `null` when the current route has no entry. Carries a small unseen-dot until the user has
  opened help at least once (discoverability, dismissed permanently on first open).
- **`HelpPanel`** (`shared/components/HelpPanel.tsx`) — built on the existing `Modal` (`size="lg"`),
  no new dialog implementation. Sections render only when present: summary → "Kako koristiti"
  (numbered) → "Dobro je znati" (tips) → "Česta pitanja" (collapsible) → role note → "Saznaj više u
  Edukaciji" link. Footer holds a "Ne prikazuj automatski" checkbox.
- **`WelcomeModal`** (`shared/components/WelcomeModal.tsx`) — 3 short slides on first sign-in: what
  Melarium is, the apiary → hive → inspection hierarchy, where to find help. Ends on "Počnimo".
- **`FirstStepsCard`** (`shared/components/FirstStepsCard.tsx`) — rendered on `/apiaries`, above the
  list. Three derived steps (napravi pčelinjak → dodaj košnicu → zabilježi prvi pregled), each with a
  direct action link. **Renders nothing once all three are satisfied** — it disappears by itself, with
  no dismissal state to store.
- **`useHelp`** (`core/hooks/useHelp.ts`) — resolves the entry for the current route, owns open state,
  the per-key auto-open decision and the localStorage flags.

### Integration points (small, deliberate edits to existing files)

- `Layout.tsx` — mount `HelpButton` in the desktop header (beside the dark-mode toggle) **and** in the
  mobile header. The mobile header is already crowded: memory of Phase 2 records that adding the
  notification bell there forced its dropdown to viewport-anchored `fixed` below `sm`. The help panel
  is a centered `Modal`, so it has no such problem — but the *icon* still needs a slot, so on mobile it
  goes beside the bell and the scan button, and if that row overflows at 360 px the command-palette
  trigger (already redundant with the sidebar on mobile) yields first. **Verify at 360 px width.**
- `EmptyState` (`shared/components/index.tsx`) — optional `onHelp` prop rendering a "Kako ovo
  funkcioniše?" text button. Wiring it into the empty states of the P1 pages is the highest-value part
  of this spec: an empty page is exactly when a user is lost, and today it's a dead end.
- `ProfilePage` — one toggle: "Automatski prikaži pomoć na novim stranicama".
- Keyboard: `?` opens the current page's help. Must not fire while focus is in an input/textarea, and
  must not collide with the existing `CommandPalette` shortcut — `CommandPalette` deliberately keeps
  its own key handling (Phase 3 note in the refactor open-items), so this needs checking against it
  rather than assuming.

### Bundle size

The registry is prose and will grow to tens of KB. The main bundle is already ~1.64 MB (459 KB gzip),
so the registry is **lazy-loaded with a dynamic `import()` on first help open** — following the
existing precedent for heavy-but-rarely-needed assets (`shared/utils/treatmentPdf.ts` lazy-imports
`pdfFont`, `QrScannerModal`/`ReceiptScanPage` lazy-import `tesseract.js`).

Note this is *not* the same thing as route-based code splitting: the app has **no** `React.lazy` on any
route today, and this spec does not introduce it. `HelpButton` needs to know only *whether* an entry
exists before the chunk loads, so the registry module exports a tiny eagerly-imported array of route
patterns (`helpRoutes.ts`, keys only, no prose) alongside the lazily-imported content.

## Backend

**None in Phase A/B.** No entity, no endpoint, no migration.

The only backend question is Phase C (optional, post-demo): if per-device dismissal turns out to be
annoying in practice — the same user gets the welcome flow again on their phone — the fix is a small
`UserPreference`-style key/value table or a `HelpSettings` JSON column on `User`, and moving the two
flags server-side. **Deliberately not done in v1**: it is one table + one endpoint to solve an
annoyance that may not materialise, and the derived checklist (the part users actually see most)
needs no persistence either way.

## Edge cases

- Route has no registry entry → **no icon**, no empty panel.
- Deep-linked/refreshed on a form page (`/beehives/new`) → manual help works; auto-open is suppressed
  on form routes.
- User signs out and a different user signs in on the same browser → flags are email-keyed, so the
  second user gets their own first-run experience. (Two users sharing one browser profile is the known
  limit of the localStorage approach — the exact trade-off Phase C would remove.)
- `localStorage` unavailable or full (Safari private mode) → every read/write is wrapped; failure means
  help simply behaves as never-dismissed. It must never throw and break the header.
- Existing users on deploy day → they have no flags, so they'd all get auto-open on every page at once.
  **Seed the flags as already-dismissed for accounts that existed before the feature** by keying off a
  one-time "help feature introduced" marker written on first load: if a user's storage has no help
  state at all *and* they already have apiaries/hives (i.e. they're plainly not new), suppress
  auto-open and just show the unseen-dot on the icon. Same grandfathering instinct as ADR-029's soft
  email verification — do not disrupt a live user base on deploy.
- Beekeeper with no assigned hives opening `/apiaries` help → the role note explains they'll see hives
  once an admin assigns them, rather than the page looking broken.
- Offline (PWA) → help works; the registry is in the precached bundle. The Edukacija deep-link may not,
  which is fine — it's a link, not content.
- Very long help text on a 360 px screen → `Modal` already caps at `max-h-[85vh]` with an internal
  scroll area, so this is handled; **verify with the longest entry** (`/treatments`).

## Out of scope (v1)

Interactive step-by-step product tours with element highlighting/coachmarks (a real dependency and a
maintenance burden — every DOM change can break a tour anchor; the panel + derived checklist covers the
need at a fraction of the cost), video walkthroughs, a searchable help index or global "search the
docs", multi-language help (ADR-023 — Bosnian only), SystemAdmin-editable help content (see the model
decision), per-field inline tooltips on every form input (the page-level panel comes first; field-level
help can be added later where a specific field proves confusing), and any analytics on which help
pages get opened.

## Phases

- **Phase A — the mechanism + P1 content.** `helpContent.ts` (P1 entries), `helpRoutes.ts`, `useHelp`,
  `HelpButton`, `HelpPanel`, header integration, `EmptyState.onHelp` wired on P1 pages.
- **Phase B — first-run + full content.** `WelcomeModal`, `FirstStepsCard`, `ProfilePage` toggle,
  remaining P2 entries, `?` shortcut. **A + B are the realistic scope for tomorrow's demo**, and A
  alone is already demonstrable if time runs short.
- **Phase C — optional, post-demo.** Server-side persistence of the two flags (see Backend).

## Acceptance criteria

- [ ] Every page in the coverage table has a help entry by the end of Phase B; a route without one
      shows no icon rather than an empty panel.
- [ ] The icon is in the same header position on every page, on desktop **and** at 360 px width.
- [ ] The panel is keyboard-accessible and screen-reader-announced — inherited from `Modal` /
      `useDialogBehavior`, not reimplemented.
- [ ] A first-time user sees the welcome flow once, then the "Prvi koraci" card, which disappears on
      its own once they have an apiary, a hive and an inspection — with nothing stored to make that happen.
- [ ] "Ne prikazuj automatski" stops auto-open permanently and does **not** stop manual opening.
- [ ] An existing (pre-deploy) user is not auto-opened help on every page after the deploy.
- [ ] A Beekeeper sees the Beekeeper role note on `/treatments` and `/members`-adjacent pages.
- [ ] The help registry is not in the initial bundle (verify in the build output that it is a separate
      chunk); main-bundle size does not regress measurably.
- [ ] `localStorage` being unavailable does not throw or break the header.
- [ ] All content Bosnian, no English fragments.
- [ ] Docs updated: `features/help-onboarding.md` (new), `context.md`. No `api-contracts.md` change —
      there is no API.

## Changed during implementation (2026-07-30)

Two things the spec did not anticipate, both found by driving the real UI in a browser:

1. **The welcome flow's open state had to move into `useHelp`.** As specced, `WelcomeModal` owned its own
   state — which meant a brand-new user's very first screen rendered the welcome modal *and* the
   auto-opened page help simultaneously: two stacked dialogs, two focus traps. `useHelp` now owns
   `welcomeOpen` and the auto-open effect is gated on it, so the page help can only fire once the intro
   is done. `WelcomeModal` became presentational.
2. **`FirstStepsCard` replaced the generic `EmptyState` on an empty `/apiaries`** for anyone who can
   create things. Rendering both said "Nema pčelinjaka / Napravi pčelinjak" immediately under "Prvi
   koraci → Napravite pčelinjak". `EmptyState` is kept for users who cannot create anything (a Beekeeper
   with no assignments), where the empty list is an assignment problem — and there it carries `onHelp`.

Also fixed in passing, because it sat directly in the new page's failure path: the admin list used
`isLoading`/`isError`, and in React Query v5 `isLoading` is `isPending && isFetching` — so between
retries neither is true while `data` is undefined, and the page rendered "Nema prijava." on a failed
request. Both new list surfaces now gate on `isPending`/`isSuccess`. The same latent pattern exists on
several older pages and was **not** touched (out of scope), but it is worth a separate pass.

## Open questions

1. **Static registry vs. DB-backed editable content** — recommendation **static**, for the four reasons
   in the model decision (ships now, can't rot, works offline, no gating questions). This is the one
   decision worth your explicit sign-off, because reversing it later means real work.
2. **Auto-open on first visit** — on by default (recommended: it's the difference between help that
   gets used and an icon nobody clicks) or opt-in only? If on by default, the pre-deploy-user
   grandfathering in Edge cases is mandatory, not optional.
3. **Welcome flow length** — 3 slides (recommended) or a single screen? More than 3 and people click
   through without reading.
4. **"Prvi koraci" steps** — the three proposed (pčelinjak → košnica → prvi pregled) or add a fourth
   (pozovi člana / QR naljepnice)? Recommendation: **three**. A fourth is irrelevant to a solo
   beekeeper, and the card should vanish quickly.
5. **Field-level tooltips** — out of scope here, but which specific fields do users actually ask you
   about? If there is a known short list (e.g. `karenca`, `LOT`, matica mark colour), those are worth a
   follow-up spec aimed precisely at them rather than a blanket tooltip pass.
6. **Should help open automatically for a brand-new user on *every* P1 page, or only on the three
   onboarding pages** (`/apiaries`, `/beehives/:id`, `/inspections/new`)? Recommendation: **only those
   three** — auto-opening on twenty pages in one session is nagging, not onboarding.
