# SPEC-10 — Apiary Migration ("Pašnjaci i selidbe")

| | |
|---|---|
| **Status** | ✅ Implemented (2026-07-04) |
| **Effort** | M (~1–2 days) |
| **Depends on** | nothing (weather/alerts follow automatically; stats gains one section) |
| **New secrets / packages** | none (map picker reuses existing `LocationPickerModal` / react-leaflet) |

## Goal

Seleće pčelarstvo: beekeepers move apiaries between pastures (bagremova paša → kadulja → livada)
through the season. Today an apiary has one fixed location, so after a physical move the weather
forecast, frost alerts, and the map all point at the wrong place — and nobody can answer "koliko je
bagrem dao ove godine?". This spec adds an org-level **pasture registry (pašnjaci)**, a **move
event with history (selidbe)**, and **per-pasture yield attribution**. Moving an apiary updates its
live coordinates, so every existing location consumer (weather, frost alert, map links) follows
automatically with zero changes.

## User stories

- As an OrganizationAdmin I maintain a registry of my pastures (naziv, lokacija na mapi, flora).
- As an OrganizationAdmin I record "Pčelinjak Sjever preseljen na Kadulju 15.06." (uz broj
  veterinarske svjedodžbe) and the apiary's weather/map immediately reflect the new location.
- As a user I see on the apiary page where it currently sits and its full move history.
- As an admin I see in Stats how many kg each pasture produced this season.

## Domain rules

- A **Pasture** is an org-scoped named location: it exists independently of apiaries and is
  reusable season after season. Multiple apiaries may sit on the same pasture simultaneously.
- An **ApiaryMove** is an event: apiary X moved **to** pasture Y at date D. `FromPastureId` is
  resolved **server-side** from the apiary's current pasture at the moment of the move (never taken
  from the client); the first move has `FromPastureId = null` ("matična lokacija").
- Executing a move sets `Apiary.CurrentPastureId = to` and **snapshots the pasture's coordinates
  into `Apiary.Latitude/Longitude`** — the single point that keeps weather, frost alerts, and map
  links working untouched. A pasture without coordinates moves the apiary "administratively"
  (coordinates stay as they were, same as today's no-location behavior).
- `MovedAt` not in the future (+1 day tolerance — Treatments precedent). Two moves of the same
  apiary on the same date are allowed (jutro/veče); ordering is `MovedAt` then `CreatedAt`.
- **Yield attribution (computed, never stored):** a harvest belongs to the pasture the apiary was
  on at `harvest.date` — i.e. the `ToPasture` of the latest move with `MovedAt <= harvest.date`;
  harvests before the first move belong to "Matična lokacija".
- Deleting a pasture is **blocked** (`Restrict` + Bosnian 400) while any apiary currently sits on
  it or any move references it — history is the point of this feature.
- Deleting a move is mistake-correction only and allowed **only for the latest move per apiary**
  (deleting mid-history would corrupt attribution); it reverts `CurrentPastureId` (and the
  coordinate snapshot) to the previous move's pasture, or to null/unchanged coordinates when it was
  the first move.

## Backend

### Entities

```
Pasture : BaseEntity                        // org-scoped pasture registry
  OrganizationId int  (FK, cascade)
  Name        string(100)
  Latitude    double?
  Longitude   double?
  Address     string(200)?
  FloraNotes  string(300)?                  // "bagrem, lipa; paša traje V–VI"
  Notes       string(500)?

ApiaryMove : BaseEntity                     // one relocation event
  ApiaryId       int   (FK, cascade)
  FromPastureId  int?  (FK, SET NULL)       // null = prva selidba s matične lokacije
  ToPastureId    int   (FK, RESTRICT)
  MovedAt        DateTime
  CertificateNumber string(50)?             // broj veterinarske svjedodžbe — legal, LOT precedent
  Notes          string(500)?
  CreatedById    int?  (FK User, SET NULL)

Apiary += CurrentPastureId int? (FK, SET NULL)
```

`IPastureRepository` (org-scoped CRUD + `HasReferencesAsync`) and `IApiaryMoveRepository`
(`GetByApiaryAsync` newest first, `GetLatestForApiaryAsync`, `GetAllForOrganizationAsync` — stats
attribution) + `IUnitOfWork` properties. Migration `AddPasturesAndMoves`.

### Endpoints

| Method | Path | Notes |
|---|---|---|
| GET | `/api/pastures` | org-scoped list (+ `apiariesOnPasture` count) — all authenticated |
| POST/PUT/DELETE | `/api/pastures[/{id}]` | OrganizationAdmin/SystemAdmin (delete → 400 while referenced) |
| GET | `/api/apiaries/{id}/moves` | move history, newest first (anyone who can view the apiary) |
| POST | `/api/apiaries/{id}/moves` | `{ toPastureId, movedAt, certificateNumber?, notes? }` → 201; OrganizationAdmin/SystemAdmin (same matrix as apiary edit) |
| DELETE | `/api/apiaries/{id}/moves/{moveId}` | latest move only (else 400); reverts pasture + coordinates |

Validation: `toPastureId` must belong to the apiary's organization; moving to the pasture the
apiary is already on → 400 *"Pčelinjak je već na ovom pašnjaku."*; `movedAt` range check.
Authorization via `IAccessGuard` (org/apiary scoping identical to apiary management).

### Stats

`GET /api/stats` gains `kgByPasture[]` (`NameDecimalDto`, current year): harvests joined to the
apiary's move timeline per the attribution rule; bucket "Matična lokacija" for pre-first-move
harvests. Reuses `IApiaryMoveRepository.GetAllForOrganizationAsync` — one query, in-memory join
(SPEC-02 aggregation precedent).

## Frontend

- Models + `pastureService.ts` / `moveService.ts` + hooks (invalidate `['pastures']`,
  `['apiaries']`, `['apiary-moves', apiaryId]`).
- **`/pastures`** (`features/pastures/PasturesPage.tsx`) — org registry: cards/table (naziv, flora,
  broj pčelinjaka trenutno na pašnjaku), create/edit form with **`LocationPickerModal`** reuse
  (react-leaflet, ApiaryFormPage pattern) + overview map with a marker per pasture. Write actions
  OrgAdmin/SystemAdmin only. Nav item **"Pašnjaci"** (lucide `Map` or `Tent`) — visible to
  OrgAdmin/SystemAdmin (Beekeepers/ApiaryAdmins reach pasture info through the apiary page).
- **`ApiaryDetailPage`**:
  - header chip with the current pasture (🏕️ "Kadulja — od 15.06.") or "Matična lokacija";
  - **"Preseli"** button (OrgAdmin/SystemAdmin) → modal: pasture select, datum, broj svjedodžbe,
    napomena;
  - "Selidbe" `CollapsibleSection` — timeline (datum, {from} → {to}, svjedodžba, ko je zabilježio),
    delete on the latest entry with confirm.
- **`StatsPage`**: "Prinos po pašnjaku" bar (kg, current year) next to the existing yield charts.
- All labels Bosnian; no new enums (no `BsLabels` changes expected).

## Integrations (all automatic or soft)

- **Weather / frost alert (SPEC-04) / map links** — zero changes: they read
  `Apiary.Latitude/Longitude`, which the move snapshots.
- **SPEC-01 advisor context** (soft): one line "Pašnjak: {name}, od {movedAt}" via
  `GetLatestForApiaryAsync` — additive, omitted when the apiary never moved.
- **Calendar** (out of scope v1): planned-move reminders belong to Todos for now.

## Edge cases

- Apiary had no coordinates and moves to a pasture with coordinates → apiary gains
  weather/map for the first time (snapshot fills them).
- Pasture coordinates edited **after** a move → apiaries currently on it keep their snapshot
  (documented; re-move to refresh). Attribution is by pasture id, so stats are unaffected.
- Apiary deleted → its moves cascade away; pasture stays.
- Harvest recorded with a date before the apiary's first move → "Matična lokacija" bucket.
- Beekeeper (read-only role) sees current pasture + history, never write actions.

## Out of scope (v1)

Per-hive moves (subset of hives to another location), move planning/reminders, transport cost
capture (ručno u Troškove), GPS tracking, pasture occupancy conflicts, pasture sharing between
organizations, offline move capture.

## Acceptance criteria

- [x] Move updates `CurrentPastureId` + coordinate snapshot; apiary weather/map show the new
      location immediately (service unit test on the snapshot; UI spot-check ostaje pri prvoj upotrebi).
- [x] `FromPastureId` resolved server-side; first move has null; move to the current pasture → 400;
      foreign-org pasture → 400/403 (matrix-tested).
- [x] Yield attribution correct across: before first move, between moves, after last move
      (unit tests on the stats attribution incl. same-day move+harvest).
- [x] Pasture delete blocked with a Bosnian message while referenced; free pasture deletes.
- [x] Deleting the latest move reverts pasture and coordinates; deleting an older move → 400.
- [x] `/pastures` write actions hidden + API 403 for ApiaryAdmin/Beekeeper (`Roles.OrgManagers`
      attribute); both roles still see the apiary's current pasture and history.
- [x] "Prinos po pašnjaku" renders in Stats with the "Matična lokacija" bucket (empty without moves).
- [x] Docs updated: `features/apiary-migration.md`, `api-contracts.md`, `context.md`, glossary
      ("pašnjak", "selidba", "svjedodžba"), this spec → ✅.
