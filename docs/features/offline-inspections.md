# Feature: Offline Inspections (Offline unos pregleda)

## Overview

Apiaries are often out of signal. Reads survive via the existing Workbox `NetworkFirst` cache;
this feature makes the highest-value **write** survive too: creating an inspection offline stores
it in a local **outbox** (IndexedDB) and syncs it automatically when the connection returns.
Frontend-only — the backend is untouched. Implemented per
[SPEC-07](../specs/SPEC-07-offline-inspections.md). Scope is create-inspection only (v1).

## Outbox store (`core/offline/outbox.ts`)

- Hand-rolled IndexedDB wrapper (no `idb` package): DB `beehive-offline`, store `outbox`,
  keyPath `localId` (`crypto.randomUUID()`).
- `OutboxItem`: `{ localId, ownerEmail, beehiveId, beehiveName, payload, createdAt, status:
  'pending'|'failed', error? }`.
- **Deviation from the spec:** items are keyed by **`ownerEmail`**, not `userId` — the client
  session (`AuthUser`/`LoginResponseDto`) carries no numeric user id and SPEC-07 forbids backend
  changes. Email gives the identical per-user isolation guarantee.
- An in-memory mirror + subscriber set backs `useSyncExternalStore` (live badge); a
  `BroadcastChannel('beehive-outbox')` refreshes other tabs' mirrors on every mutation
  (IndexedDB has no change events).
- Entries are plaintext in the browser profile — same trust level as the stored refresh token
  (ADR-009). Logout does **not** clear the outbox: items survive token expiry and sync resumes
  after the next login; another user on the same device never sees them (email filter).

## Sync engine (`core/offline/syncOutbox.ts`)

- `flushOutbox(ownerEmail)`: the owner's `pending` items, **oldest first**, sequential POSTs via
  the normal `inspectionService.create` — auth refresh, interceptors, and validation behave exactly
  like online submits.
  - `201` → remove item; caller invalidates `['inspections']` / `['beehives']`.
  - HTTP error **with** a response (400/403/404…) → mark `failed` + store the API's Bosnian message
    (auto-retry would loop forever; the user edits or discards).
  - Network error (no response) → stop the flush, item stays `pending`.
- **Single-flight**: per-tab via a module-level promise; **cross-tab** via
  `navigator.locks.request('beehive-outbox-flush', { ifAvailable: true })` — a second tab resolves
  immediately with zeros (Web Locks unsupported → per-tab guard only).
- Triggers (`core/offline/useOutboxSync.ts`, mounted once in `Layout` — renders behind
  `ProtectedRoute`, so auth is resolved): app mount, `window 'online'`, and the manual
  "Pošalji sada" button. Successes toast "Sinhronizovano {n} pregleda."

## Capture (`InspectionFormPage`, create mode only)

- Submit: `!navigator.onLine` pre-check **or** axios error without a response (airplane mode
  mid-request) → write outbox item → toast *"Nema mreže — pregled je sačuvan lokalno i biće poslan
  automatski."* → navigate back. HTTP rejections with a response render exactly as before.
- `beehiveName` for the offline list comes from the TanStack cache (`queryKeys.beehive`), falling
  back to `Košnica #{id}`.
- **Edit-from-outbox**: `/inspections/new?beehiveId=&outboxId=` prefills the form from the item;
  a successful online save removes the item; an offline save **updates** it (failed → back to
  pending, error cleared) instead of duplicating.
- Voice input requires the network (server transcription): the mic button is disabled offline with
  the hint *"Glasovni unos zahtijeva mrežu."*

## UI

- `Layout`: amber offline banner ("Radiš offline — izmjene se čuvaju lokalno.") via
  `useOnlineStatus()` + a `CloudOff` badge with the item count linking to `/outbox` (desktop header
  and mobile menu).
- `OutboxPage` (`/outbox`, `features/offline/`): items with hive name, dates, status badge
  (Na čekanju / Odbijeno + API message), per-item **Uredi**/**Obriši** (confirm), and a global
  **"Pošalji sada (n)"** disabled offline.
- `BeehiveDetailPage`: amber hint card when the hive has unsent items, linking to `/outbox`.

## Edge cases & known trade-offs

- Duplicate protection: an item is removed only after a `201`; a crash between the `201` and the
  removal can double-submit in theory — accepted v1 (inspections are not unique-constrained).
- Hive deleted while pending → sync gets 404/400 → item `failed` with message; user discards.
- `payload.date` is whatever the user chose offline (clock skew is irrelevant — unchanged semantics).
- Workbox read-side: the existing `urlPattern: /\/api\//i` + `NetworkFirst` rule covers all API
  GETs (`apiaries`, `beehives`, `inspections`…); Workbox routes match **GET only** by default, so
  the service worker never intercepts the POSTs the outbox manages.

## Tests

The frontend has no test runner (adding vitest + fake-indexeddb needs package approval), so the
outbox wrapper ships with a **manual dev harness**: `core/offline/outboxSelfTest.ts`, exposed in dev
builds as `window.__outboxSelfTest`. Run `await __outboxSelfTest()` in the browser console — it
exercises enqueue/pending status, oldest-first ordering, per-owner isolation, failed-status
persistence, and idempotent removal against real IndexedDB with sentinel owners, then cleans up.
Manual E2E (DevTools offline → create → auto-sync on reconnect; one real phone) per the spec's
acceptance list.
