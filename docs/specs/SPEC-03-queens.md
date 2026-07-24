# SPEC-03 — Queen Tracking ("Matice")

| | |
|---|---|
| **Status** | ✅ Implemented (2026-07-02); edit history added 2026-07-04 |
| **Effort** | S/M (~1 day) |
| **Depends on** | nothing |
| **New secrets / packages** | none |

## Goal

Track the queen (matica) per hive: age, marking color, origin, and replacement history. Queen age
is the single strongest predictor of colony strength and swarming — serious beekeepers replace
queens every 2–3 seasons and need to know *which* hive is due. Also feeds SPEC-01 (advisor
context) and SPEC-04 (old-queen alert).

## Domain rules

- A hive has **at most one Active queen**; full history is kept.
- Registering a new queen on a hive **auto-closes** the previous active one
  (status → `Replaced`, `EndDate` = new queen's `IntroducedDate`) in the same transaction.
- International marking color derives from year of birth (editable, since practice varies):
  year ending **1/6 = White, 2/7 = Yellow, 3/8 = Red, 4/9 = Green, 5/0 = Blue**.
  The derivation lives in one static helper (`QueenMarkColorHelper.ForYear(int)`) used by both
  the form default and the seeded data.

## Backend

### Entity

```
Queen : BaseEntity
  BeehiveId       int   (FK, cascade delete)
  Year            int                       // year of birth, e.g. 2026
  MarkColor       QueenMarkColor enum { White=1, Yellow=2, Red=3, Green=4, Blue=5 }
  IsMarked        bool                      // physically marked?
  IsClipped       bool                      // wing clipped?
  Origin          QueenOrigin enum { Kupljena=1, VlastitiUzgoj=2, Rojenje=3, TihaZamjena=4, Nepoznato=99 }
  Status          QueenStatus enum { Active=1, Replaced=2, Uginula=3, Nestala=4 }
  IntroducedDate  DateTime
  EndDate         DateTime?                 // set when status leaves Active
  Notes           string(500)?
```

`IQueenRepository` (+ `IUnitOfWork`): `GetByBeehiveAsync(beehiveId)` (history, newest first),
`GetActiveByBeehiveIdsAsync(ids)` (projection for lists/context/alerts). Migration `AddQueens`.
Unique filtered index: one `Active` queen per `BeehiveId` (Postgres partial index — enforce the
invariant in the DB, not just the service).

### Edit history (correcting mistakes)

`PUT /queens/{id}` also fixes wrong initial data — the form supports editing the **active** queen
directly (not just closed history rows). Every field-level change made through `PUT` is recorded as
an append-only audit row, so a correction stays traceable:

```
QueenEditLog : BaseEntity
  QueenId      int    (FK, cascade delete)
  EditedById   int?   (FK → User, SetNull — keeps the log row if the user is later deleted)
  FieldLabel   string(100)   // Bosnian label snapshotted at edit time, e.g. "Godište"
  OldValue     string(500)?
  NewValue     string(500)?
```

`QueenService.UpdateAsync` diffs the incoming DTO against the tracked entity field-by-field
(Year, MarkColor, IsMarked, IsClipped, Origin, Status, IntroducedDate, EndDate, Notes) before
mutating it, and writes one `QueenEditLog` row per changed field in the same `SaveChangesAsync`
(no-op fields are skipped, so a save with no actual changes writes nothing). Migration
`AddQueenEditLogs`.

### Endpoints

| Method | Path | Notes |
|---|---|---|
| GET | `/api/beehives/{beehiveId}/queens` | history → `QueenDto[]` |
| POST | `/api/beehives/{beehiveId}/queens` | `{ year, markColor?, isMarked, isClipped, origin, introducedDate, notes? }` — auto-replaces active; `markColor` defaults from year |
| PUT | `/api/queens/{id}` | edit any field incl. status (active or historical row); setting status ≠ Active requires/auto-sets `EndDate`; records a `QueenEditLog` row per changed field |
| GET | `/api/queens/{id}/history` | field-level edit log for one queen record → `QueenEditLogDto[]`, newest first |
| DELETE | `/api/queens/{id}` | mistakes only; if the deleted one was Active, hive simply has no active queen |

Authorization via `IAccessGuard`: same rights as **editing the hive** (read = viewing the hive).
Validation: `year` between 2000 and current year (+0); `introducedDate` not future; `endDate ≥ introducedDate`.

## Frontend

- Models + `queenService.ts` + hooks (`useQueens(beehiveId)`, mutations invalidate `['queens', beehiveId]`
  and `['beehives', id]`).
- `BeehiveDetailPage`: **"Matica" card** — active queen: color dot (real CSS color), year + computed
  age ("3. sezona"), origin label, marked/clipped chips; buttons "Uredi" (edit the active record
  in place — for fixing wrong initial data) and "Zamijeni maticu" (form modal, pre-filled defaults:
  current year, derived color, today) and "Historija" (modal list with status timeline). No active
  queen → empty-state card with "Dodaj maticu".
- "Historija" modal: each row also has a clock icon opening **"Historija izmjena"** — a per-record
  audit log (field, old → new value, timestamp, editor name) backed by `GET /queens/{id}/history`.
- Age display rule (shared util): `season = currentYear - year + 1` → "1. sezona" / "2. sezona"…;
  ≥ 3 renders with a warning tint (visual nudge; the actual alert is SPEC-04).
- All enum labels Bosnian (backend `BsLabels` + frontend label maps, per house pattern).

## Integrations (soft — implement here, consumed elsewhere)

- **SPEC-01 advisor context:** add "Matica: {year}, {n}. sezona, {status/origin}" to
  `AdvisorContextBuilder` if SPEC-01 already shipped (one section, ~2 lines of code).
- **SPEC-04 alert:** exposes `GetActiveByBeehiveIdsAsync` — the old-queen rule needs only this.

## Out of scope (v1)

Breeding/rearing module (grafting, mating nucs), queen lineage/genetics, per-queen performance
correlation, queen marketplace. Column in the hive **list** view (detail page only for now).

## Acceptance criteria

- [x] Adding a queen to a hive with an active one closes the old one atomically (unit test on the
      service; verified live against local PostgreSQL — replace sets `Replaced` + `EndDate` in one save;
      re-activating while another queen is active returns `422`).
- [x] Color defaults correctly (e.g. 2026 ends in 6 → White; 2027 → Yellow).
      Unit-test `QueenMarkColorHelper` for all 10 digits (+ live check: 2024 → Zelena, 2026 → Bijela).
- [x] History shows the full timeline with correct end dates and Bosnian labels
      (API verified live; UI built and type-checked — visual pass still recommended).
- [x] A Beekeeper without access to the hive gets `403/404` on all queen endpoints
      (verified live: foreign-org Beekeeper → `403`; guard reuses `EnsureCanAccessBeehiveAsync`,
      already covered by the AccessGuard test matrix).
- [x] Docs updated: `features/queens.md`, `api-contracts.md`, `context.md`, glossary ("matica",
      "tiha zamjena"), this spec → ✅.
- [x] The active queen can be edited in place via "Uredi" (no need to go through "Historija" to
      fix a typo in the initial data).
- [x] Every field-level edit made via `PUT /queens/{id}` is recorded and viewable per queen record
      via "Historija izmjena" (old value → new value, timestamp, editor).
