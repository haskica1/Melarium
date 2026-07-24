# Feature: Apiary Migration (Pašnjaci i selidbe)

## Overview

Migratory beekeeping support (SPEC-10): an org-level **pasture registry (pašnjaci)**, **move events
(selidbe)** with history, and **per-pasture yield attribution**. The core design decision: a move
**snapshots the pasture's coordinates into `Apiary.Latitude/Longitude`**, so every existing
location consumer — weather forecast, frost alerts (SPEC-04), map links — follows the relocation
with zero changes to their code. Implemented per [SPEC-10](../specs/SPEC-10-apiary-migration.md).

## Domain rules

- `Pasture` is org-scoped and independent of apiaries (name, optional coordinates — both or
  neither, address, `FloraNotes`, notes). Multiple apiaries may sit on one pasture simultaneously.
- `ApiaryMove`: apiary → pasture at `MovedAt`, with optional **`CertificateNumber`** (broj
  veterinarske svjedodžbe — legal expectation, LOT precedent) and notes. **`FromPastureId` is
  resolved server-side** from `Apiary.CurrentPastureId`; first move → null ("matična lokacija").
- Move execution (one `SaveChangesAsync`): sets `Apiary.CurrentPastureId` + coordinate snapshot
  (skipped when the pasture has no coordinates — an "administrative" move).
- `MovedAt` not in the future (+1 day tolerance); moving to the pasture the apiary is already on →
  400 *"Pčelinjak je već na ovom pašnjaku."*; pasture from another org → 400.
- **Yield attribution (computed, never stored)** — `PastureAttribution.ResolveToPastureId` in
  `Domain/Common` (single source, unit-tested): a harvest belongs to the `ToPasture` of the latest
  move with `MovedAt <= harvest.date` (same-day move → the **new** pasture); nothing before →
  "Matična lokacija" bucket.
- Pasture delete **blocked** (400, Bosnian message) while any apiary sits on it or any move
  references it (`HasReferencesAsync`); DB backstop: `ToPastureId` is `Restrict`.
- Move delete = mistake correction, **latest move only** (deleting mid-history would corrupt
  attribution); reverts `CurrentPastureId` and snapshots the previous pasture's coordinates back.
  Reverting the **first** move keeps coordinates as-is — the original location was never stored
  (documented trade-off; user re-sets the apiary location manually if needed). Editing a pasture's
  coordinates later does **not** touch apiaries already on it (snapshot semantics; re-move to refresh).

## API

| Method | Path | Notes |
|---|---|---|
| GET | `/api/pastures` | org list + `apiariesOnPasture` count — all authenticated roles |
| POST/PUT/DELETE | `/api/pastures[/{id}]` | `[Authorize(Roles = OrgManagers)]`; delete → 400 while referenced |
| GET | `/api/apiaries/{id}/moves` | history, newest first — apiary-view access (Beekeeper via assigned hives) |
| POST | `/api/apiaries/{id}/moves` | `{ toPastureId, movedAt, certificateNumber?, notes? }` → 201; OrgManagers |
| DELETE | `/api/apiaries/{id}/moves/{moveId}` | latest only (else 400); reverts pasture + coordinates; OrgManagers |

`GET /api/stats` gains `kgByPasture[]` (`NameDecimalDto`, current year, "Matična lokacija" bucket) —
**empty when the organization has no moves**, so non-migratory users see no noise.

## Integrations

- **Weather / frost alert / map links** — untouched by design (coordinate snapshot).
- **Advisor context (SPEC-01)**: one line "Pašnjak: {name}, od {datum}" (preformatted `pastureLine`,
  weather-line precedent), omitted when the apiary never moved.

## UI

- Nav **"Pašnjaci"** (`Tent` icon) for OrgAdmin/SystemAdmin; the `/pastures` route itself is open to
  all authenticated (write actions hidden + API 403 for others).
- `PasturesPage` (`features/pastures/`) — registry cards (naziv, flora, broj pčelinjaka), overview
  **map** (react-leaflet markers + popups), form modal reusing **`LocationPickerModal`**.
- `ApiaryDetailPage` — current-pasture chip in the hero ("⛺ Kadulja · od 15.06.", only when moves
  exist), `ApiaryMovesSection` ("Selidbe" collapsible: timeline {from} → {to} + svjedodžba, delete
  on the latest entry only) and the **"Preseli"** modal (pasture select excluding the current one,
  datum, svjedodžba, napomena).
- `StatsPage` — "Prinos po pašnjaku (kg)" horizontal bar in the harvests section, rendered only
  when `kgByPasture` is non-empty.

## Edge cases

- Apiary without coordinates moving onto a located pasture gains weather/map for the first time.
- Pasture without coordinates → administrative move (apiary coordinates unchanged, same as today's
  no-location behavior).
- Apiary deleted → its moves cascade; pasture survives. Harvest dated before the first move →
  "Matična lokacija" in stats.

## Tests

`PastureAttributionTests` — before/between/after moves, same-day move+harvest → new pasture,
same-day double move → later `CreatedAt` wins, no moves → null. `ApiaryMoveServiceTests` —
server-side From resolution + coordinate snapshot, first-move null From, same-pasture and
foreign-org 400 (nothing saved), pasture-without-coords keeps coordinates, delete-latest reverts
pasture + coordinates, delete-older 400, delete-first-move reverts to null pasture with coordinates
kept. `PastureServiceTests` — referenced-pasture delete blocked, free pasture deletes, org-less
caller gets an empty registry.
