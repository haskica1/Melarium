# In-app help & onboarding — Pomoć u aplikaciji (SPEC-14)

> Implemented 2026-07-30. Spec: [`specs/SPEC-14-in-app-help.md`](../specs/SPEC-14-in-app-help.md).
> **Frontend only** — no entity, no endpoint, no migration.

## What it is

Per-page contextual help behind one info icon that sits in the same header position on every page,
plus a short first-run welcome flow and a "Prvi koraci" checklist for new beekeepers.

## Three decisions that shape it

### 1. Help content lives in code, not in the database

`frontend/src/core/help/helpContent.ts` is a typed registry, one entry per page.

Help text that describes a UI *is documentation of code*: when `TreatmentFormPage` changes, its help
text has to change in the same commit or it starts lying to users. A database copy has no mechanism to
notice the UI moved. It also works offline (it's in the bundle), and it needs no read/write
authorization or plan-gating decisions.

Edukacija (SPEC-06) remains the DB-backed CMS for long-form, admin-authored beekeeping knowledge. The
division of labour:

> Short, UI-coupled "kako radi ova stranica" → static registry (this feature).
> Long-form, seasonal "kako se pčelari" → Learning Topics (SPEC-06).

The panel therefore *links* to the relevant Edukacija category instead of duplicating it — by
**category, never by topic id**, because ids are database-generated and differ per environment.
(`LearningPage` seeds its initial category filter from `?category=` for this.)

### 2. One icon in `Layout`, not thirty icons in thirty pages

`useHelp()` is called once, in `Layout`, and resolves the entry from the current route via `matchPath`.
Around thirty routes would otherwise each need a manual edit, and any page whose author forgot would
silently have no help — the inconsistency the feature exists to remove.

A route with no entry renders **no icon at all**, so a missing entry degrades to the previous behaviour
rather than an empty popup.

Pages inside `<Outlet />` reach the single panel through `HelpProvider` / `useHelpTrigger()` — used by
`EmptyState`'s optional `onHelp` prop, which is where help pays off most: an empty page is exactly when
a user is lost.

### 3. Onboarding progress is derived, never stored

`FirstStepsCard` computes its three steps from data the app already has — has an apiary, has a hive,
has an inspection — rather than a persisted `onboardingCompleted` flag. Same reasoning as the computed
effective plan in ADR-028: deriving state avoids a whole class of "we forgot to flip the flag" bugs,
and it behaves correctly where a flag would be wrong (someone added to an organisation that already
has apiaries is not told to create their first one).

Consequence: **no table, no migration, no endpoint.** The card removes itself once all three are done,
so there is no dismissal state either. The third step needs a count the apiary list doesn't carry, so
`useStats` is called only once the first two are already satisfied.

## Files

| File | Role |
|---|---|
| `core/help/helpRoutes.ts` | Route patterns, **most specific first** (so `/apiaries/new` beats `/apiaries/:id`), `resolveHelpKey`, `AUTO_OPEN_KEYS`. Keys only — eagerly imported |
| `core/help/helpContent.ts` | The prose. **Lazily imported** on first help open |
| `core/help/helpStorage.ts` | Per-user localStorage flags, every access wrapped |
| `core/help/useHelp.ts` | Route resolution, panel state, welcome state, auto-open, `?` shortcut |
| `core/help/HelpContext.tsx` | Lets pages open the panel `Layout` owns |
| `shared/components/HelpButton.tsx` | The icon (`header` and `mobile` variants) + unseen dot |
| `shared/components/HelpPanel.tsx` | Built on the shared `Modal` |
| `shared/components/WelcomeModal.tsx` | Three slides; presentational, open state owned by `useHelp` |
| `shared/components/FirstStepsCard.tsx` | Derived checklist, rendered on `/apiaries` |
| `features/profile/HelpPreferenceSection.tsx` | The one preference |

## Behaviour

- **Coverage:** every authenticated page in the route table has an entry. No entry for the auth pages
  or the public QR scan — they are outside `Layout`, so there is no header to host the icon.
- **Auto-open:** the first visit to one of the three core pages (`/apiaries`, `/beehives/:id`,
  `/inspections/new`) opens help by itself, at most once per page. An icon nobody clicks helps nobody.
- **Welcome flow** is shown once per (user, browser) and doubles as the announcement of this feature for
  people who were already using Melarium. Its open state lives in `useHelp` so it can **hold back** the
  page auto-open — otherwise a new user's first screen stacked two dialogs and two focus traps.
- **Grandfathering:** "Preskoči uvod" pre-marks all three auto-open pages as seen. That is what keeps
  existing users from being ambushed by a modal on every core page after the deploy — the same
  don't-disrupt-a-live-user-base instinct as ADR-029's soft e-mail verification.
- **"Ne prikazuj automatski"** (in the panel footer and on `ProfilePage`) stops *automatic* opening
  only. Manual opening always works.
- **`?`** opens the current page's help, ignored while focus is in an input/textarea/select or a
  contenteditable so it cannot hijack a form.
- **Role notes:** an entry may carry `notesByRole`, because the same page genuinely means different
  things per role (a Beekeeper is read-only on Tretmani, an OrgAdmin is not).
- Accessibility (Escape, focus trap, focus restore, scroll lock) is inherited from `Modal` /
  `useDialogBehavior` — not reimplemented.

## Bundle

`helpContent` is a **separate chunk** (~17 kB raw, ~6.5 kB gzip), loaded on first help open via a
dynamic `import()` — the existing convention for heavy-but-rarely-needed assets (`pdfFont`,
`tesseract.js`). `helpRoutes` stays eager because the button must know whether an entry exists on every
render. This is **not** route-level `React.lazy`; the app does not route-split and this feature does not
introduce it.

## Storage keys

All keyed by the signed-in user's e-mail (the client session has no numeric user id — the same reason
the offline outbox keys by e-mail):

```
melarium-help-welcome:{email}   melarium-help-autoopen:{email}
melarium-help-opened:{email}    melarium-help-used:{email}
```

Per browser, not per account — a known limit (the same user gets the first-run experience again on
their phone). Moving the flags server-side is the documented Phase C, deliberately not done for v1.
Every read and write is wrapped: in Safari private mode `localStorage` can throw, and a help preference
must never break the header it lives in.
