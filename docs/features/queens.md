# Feature: Queens (Matice)

## Overview

Tracks the queen bee (matica) per beehive: the currently active queen plus the full replacement
history. Queen age (season) is the main signal for replacement decisions; the marking color follows
the international year-color code.

## Domain Rules

- A beehive has **at most one Active queen** — enforced both in `QueenService` and by a partial
  unique index in the database (`IX_Queens_BeehiveId_ActiveUnique`, on `BeehiveId` where `Status = 1`)
- Registering a new queen (`POST`) always creates it as `Active`; an existing active queen is
  automatically closed as `Replaced` with `EndDate` = new queen's `IntroducedDate`, **in the same
  `SaveChangesAsync`** (atomic)
- `MarkColor` defaults from `Year` when omitted (international code: years ending 1/6 White,
  2/7 Yellow, 3/8 Red, 4/9 Green, 5/0 Blue) — `QueenMarkColorHelper.ForYear` (Domain), mirrored by
  `queenColorForYear` on the frontend
- `Status` values: `Active`, `Replaced`, `Died` (uginula), `Missing` (nestala)
- Setting a non-Active status via `PUT` sets `EndDate` (defaults to now when omitted); setting a
  queen back to `Active` clears `EndDate` and is rejected with a 422 business rule error if the
  hive already has a different active queen
- `Origin` values: `Purchased`, `OwnBreeding`, `Swarm`, `Supersedure` (tiha zamjena), `Unknown`
- Deleting a beehive cascades to its queens; deleting a queen record is allowed (mistake correction)
- DTOs carry Bosnian `*Name` labels (`BsLabels`); mapping is **manual** in the service
  (computed-label DTO, same policy as Diets/Admin)
- `PUT` diffs every field (Year, MarkColor, IsMarked, IsClipped, Origin, Status, IntroducedDate,
  EndDate, Notes) against the tracked entity before mutating it, and writes one `QueenEditLog` row
  per changed field (old value, new value, Bosnian field label, editor, timestamp) in the same
  `SaveChangesAsync` — a no-op save writes nothing

## API

- `GET /beehives/{beehiveId}/queens` — full history, newest introduction first
- `POST /beehives/{beehiveId}/queens` — register new active queen (auto-replaces the current one)
- `PUT /queens/{id}` — edit any field including status changes (works on the active row too, for
  correcting wrong initial data); records a `QueenEditLog` row per changed field
- `GET /queens/{id}/history` — field-level edit log for one queen record → `QueenEditLogDto[]`, newest first
- `DELETE /queens/{id}` — hard delete

## Access

Same rule as inspections/diets/todos: `IAccessGuard.EnsureCanAccessBeehiveAsync` for **all**
operations — managers within scope, or a Beekeeper assigned to the hive (queen replacement is a
field operation performed by whoever works the hive).

## UI Rules

- "Matica" card in the `BeehiveDetailPage` sidebar (`QueenSection`, top of the sidebar column)
- Active queen shows: mark-color dot (real color), year + season badge (`queenSeason`: birth year
  = 1st season), origin, Označena/Podrezana krila chips, introduced date, notes
- Season ≥ 3 renders an amber warning ("razmisli o zamjeni")
- "Uredi" (visible when an active queen exists) opens the same form modal pre-filled with the
  active queen's data, for correcting wrong initial data in place
- "Zamijeni maticu" opens the form modal (year defaults to current year, color auto-derived while
  untouched); a hint explains the current queen will be auto-closed
- "Historija (n)" opens a modal with the full timeline (status badge, date range, per-row
  edit/delete for users who can manage the hive, plus a clock icon open to anyone with hive
  access for "Historija izmjena" — the per-row field-level audit log)
- Status/EndDate fields appear only in **edit** mode of the form

## Edge Cases

- Multiple non-active queens with overlapping dates are allowed (history is a log, not a schedule)
- A hive may have no active queen (died/missing without replacement) — the card shows an
  empty-state with "Dodaj maticu"
- The DB partial index makes concurrent double-activation impossible even under races

## Validation Rules

```
year:            required, 2000..current year
markColor:       optional on create (derived from year), valid enum
origin:          required, valid enum
status (PUT):    required, valid enum
introducedDate:  required, not in the future
endDate (PUT):   optional, must be >= introducedDate
notes:           optional, max 500 chars
```

## Tests

`QueenMarkColorHelperTests` (color code for all 10 year digits) and `QueenServiceTests`
(first queen active + derived color, atomic replace in one save, double-active rejection,
end-date defaulting, access-guard enforcement, not-found paths, edit-log rows written on change
and skipped when nothing changed).
