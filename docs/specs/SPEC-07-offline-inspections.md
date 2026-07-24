# SPEC-07 — Offline Inspections ("Offline unos pregleda")

| | |
|---|---|
| **Status** | ✅ Implemented (2026-07-03) — manual E2E (offline DevTools + telefon) ostaje za ručnu provjeru |
| **Effort** | M/L (~2–3 days, mostly frontend) |
| **Depends on** | nothing (backend unchanged except nothing at all — this is frontend-only) |
| **New packages** | none (hand-rolled IndexedDB wrapper ≈ 80 lines; `idb` only if that proves painful — ask first) |

## Goal

Apiaries are often out of signal. Reads mostly survive today (Workbox `NetworkFirst`, 24 h cache),
but **creating an inspection offline fails and the data is lost**. This spec adds a local
**outbox**: the inspection is saved on-device immediately and synced automatically when the
connection returns. Scope is deliberately narrow: **create inspection only** — the highest-value
field write.

## Design

### Outbox store (IndexedDB)

`core/offline/outbox.ts` — minimal typed wrapper (open DB `beehive-offline`, store `outbox`,
keyPath `localId`):

```ts
interface OutboxItem {
  localId: string;            // crypto.randomUUID()
  userId: number;             // owner — flush only when this user is logged in
  beehiveId: number;
  beehiveName: string;        // display only (list must render offline)
  payload: CreateInspectionPayload;
  createdAt: string;          // ISO
  status: 'pending' | 'failed';
  error?: string;             // Bosnian message for 'failed'
}
```

localStorage is not used (multi-tab safety, size limits); no service-worker background sync —
sync runs in the app (`workbox-background-sync` was considered and rejected: replaying POSTs from
the SW bypasses the axios auth/refresh interceptor and gives no UI feedback — record in `decisions.md`).

### Capture (InspectionFormPage — create mode only)

Submit flow becomes: try normal `useCreateInspection` → on **network-level failure**
(`!navigator.onLine` pre-check, or axios error with no `response`) → write `OutboxItem` → success
toast *"Nema mreže — pregled je sačuvan lokalno i biće poslan automatski."* → navigate back.
HTTP errors with a response (400/403/…) behave exactly as today — those are real rejections, not
offline. Voice input offline: mic button disabled with hint *"Glasovni unos zahtijeva mrežu"*
(transcription is a server call; queuing audio blobs is out of scope).

### Sync engine (`core/offline/syncOutbox.ts`)

- Triggers: app mount (after auth resolves), `window 'online'` event, manual "Pošalji sada" button.
  Single-flight guard (module-level promise) — never two concurrent flushes.
- Flush: items for the **current userId**, oldest first, sequential POSTs via the normal
  `inspectionService.create` (so auth refresh, interceptors, and validation behave identically):
  - `201` → remove item, invalidate `['inspections']`/`['beehives']` queries.
  - Response with 4xx → mark `failed` + store the API's Bosnian message (user must edit/discard —
    auto-retry would loop forever).
  - Network error → stop the flush (still offline); item stays `pending`.
- After a flush with successes: toast "Sinhronizovano {n} pregleda."

### UI

- **Global indicator** in `Layout.tsx`: offline banner ("Radiš offline — izmjene se čuvaju lokalno")
  driven by `useOnlineStatus()` (new `core/hooks/` hook: `online`/`offline` listeners) + a badge with
  pending count next to it (subscribe via a tiny event emitter or `useSyncExternalStore` on the outbox).
- **Outbox page** `/outbox` (`features/offline/OutboxPage.tsx`): list items (hive name, created
  time, status, error), actions per item: "Pošalji sada" (pending), "Uredi" (opens inspection form
  prefilled → on save goes through normal flow and removes the item), "Obriši" (confirm). Reached
  from the badge; also show a hint card on the hive page when it has pending items.

### Read-side hardening (small)

Verify in `vite.config.ts` that Workbox runtime caching covers `GET /api/apiaries*`,
`/api/beehives*`, `/api/inspections*` with `NetworkFirst` (they should already — fix patterns if
not). Bump nothing else; TanStack Query keeps rendering cached data while offline.

## Security & edge cases

- Outbox is per-user (`userId` filter); logout does **not** clear it (data survives token expiry —
  user logs back in, sync resumes), but another user on the same device never sees/flushes items
  that aren't theirs. Document that entries are plaintext in the browser profile (same trust level
  as the existing localStorage refresh token — see ADR-009).
- Duplicate protection: item is removed only after a `201`; a crash between `201` and removal can
  double-submit in theory — acceptable v1 (inspections aren't unique-constrained; note in doc).
- Hive deleted while item pending → sync gets 404/400 → item marked `failed` with message; user discards.
- Clock skew: `payload.date` is whatever the user chose offline — unchanged semantics.

## Out of scope (v1)

Offline edits/deletes; offline creation of todos/diets/harvests; queuing voice audio; conflict
resolution; server idempotency keys; Background Sync API; precaching hive lists proactively
("download apiary for offline") — good future spec.

## Acceptance criteria

- [ ] DevTools offline → create inspection → banner + toast + item visible in `/outbox`; going
      online auto-syncs, list invalidates, inspection appears on the hive (manual E2E on desktop + one real phone).
      *(Implementirano; ručna E2E provjera na desktopu i telefonu još nije izvršena — jedino preostalo.)*
- [x] Airplane-mode mid-request (request sent, no response) also lands in the outbox, not lost
      (axios error without `response` → `isNetworkError` → outbox).
- [x] 400 from server on sync → item `failed` with the API message; "Uredi" path clears it correctly
      (`?outboxId=` prefill → uspješan save briše item; offline save ga ažurira umjesto dupliciranja).
- [x] Two tabs open: flush happens once, no duplicate inspections (Web Locks
      `beehive-outbox-flush` + per-tab single-flight; drugi tab odmah vraća nulu).
- [x] Second user on the same browser sees zero items from the first user (email filter; pokriveno
      self-testom per-owner izolacije).
- [x] Voice button correctly disabled offline; form otherwise fully usable.
- [x] TypeScript clean, no `any`; outbox wrapper has focused unit tests — **manual dev harness**
      `await __outboxSelfTest()` (frontend nema test runner; vitest/fake-indexeddb = novi paketi).
- [x] Docs updated: `features/offline-inspections.md`, `context.md`, `decisions.md` (ADR-026
      outbox-over-SW-sync), this spec → ✅. *(Devijacija: `ownerEmail` umjesto `userId` — klijentska
      sesija nema numerički id, a backend se ne mijenja.)*
