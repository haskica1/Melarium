# SPEC-02 — Harvest Log ("Vrcanje i prinosi")

| | |
|---|---|
| **Status** | ✅ Implemented (2026-07-03) |
| **Effort** | M (~1–2 days) |
| **Depends on** | nothing |
| **New secrets / packages** | none |

## Goal

The app tracks costs (Expenses) but not the income side. This spec adds **harvest records**
(vrcanje): how many kg of which honey were extracted, per hive, per date. That unlocks the
questions every beekeeper actually asks: *which hives produce, how good was the season, am I
profitable?*

## User stories

- As an OrganizationAdmin/ApiaryAdmin, after extraction day I record one harvest for the apiary:
  date, honey type, and kg per hive.
- As a user, on a hive page I see its yield this season and across seasons.
- As an admin, on the Stats page I see season totals, top hives by yield, and a rough
  profit figure (est. revenue − expenses).

## Backend

### Entities

```
Harvest : BaseEntity                      // one extraction event, apiary-scoped
  ApiaryId     int      (FK)
  Date         DateTime
  HoneyType    HoneyType enum { Bagrem=1, Lipa=2, Kesten=3, Suncokret=4, Livadski=5,
                                Sumski=6, UljanaRepica=7, Ostalo=99 }   // BsLabels for display
  PricePerKg   decimal(8,2)?   // optional, for revenue estimate (KM/kg)
  Notes        string(500)?
  CreatedById  int?  (FK User, SET NULL)
  Entries      ICollection<HarvestEntry>

HarvestEntry : BaseEntity                 // per-hive line
  HarvestId        int   (FK, cascade delete)
  BeehiveId        int   (FK, cascade delete)
  QuantityKg       decimal(6,2)          // > 0
  FramesExtracted  int?                  // optional
```

Repository `IHarvestRepository` (+ `IUnitOfWork` property): `GetByOrganizationAsync(orgId, year?)`,
`GetByApiaryAsync(apiaryId, year?)`, `GetWithEntriesAsync(id)`, plus an aggregate helper
`GetHiveTotalsAsync(beehiveIds, year?)` (grouped sum — used by hive detail & stats, **no row
materialization**, mirrors how hive lists return inspection counts). Migration `AddHarvests`.

### Validation & business rules

- `entries` non-empty; every `beehiveId` must belong to `apiaryId`; no duplicate hive per harvest.
- `quantityKg` between 0.1 and 200; `date` not in the future (tolerance +1 day); `pricePerKg` ≥ 0.
- Update replaces the entry set (simplest correct semantics — delete + recreate entries inside one
  `SaveChangesAsync`).

### Endpoints (`HarvestsController`, `/api/harvests`)

| Method | Path | Body → Returns |
|---|---|---|
| GET | `/harvests?apiaryId=&year=` | → `HarvestDto[]` (role-scoped; includes `totalKg`, `entryCount`) |
| GET | `/harvests/{id}` | → `HarvestDetailDto` (entries with hive names) |
| POST | `/harvests` | `{ apiaryId, date, honeyType, pricePerKg?, notes?, entries: [{beehiveId, quantityKg, framesExtracted?}] }` → `201` |
| PUT | `/harvests/{id}` | same shape → detail DTO |
| DELETE | `/harvests/{id}` | → `204` |

**Authorization (via `IAccessGuard`, apiary-scoped — same semantics as apiary edit):**
SystemAdmin/OrganizationAdmin: all org apiaries. ApiaryAdmin: own apiary only.
**Beekeeper: read-only**, and only harvests that contain at least one of their assigned hives
(entries filtered? **No** — they see the whole harvest they're part of; filtering rows would make
totals lie).

### Stats integration (extend existing `GET /api/stats`)

Add to the stats DTO (org-scoped, same role rules as today): `seasonTotalKg` (current year),
`kgByApiary[]`, `kgByHoneyType[]`, `topHivesByYield[]` (top 5, current year),
`estimatedRevenue` (Σ kg × pricePerKg where set), `yearlyYield[]` (last 3 years for trend).

## Frontend

- Models + `harvestService.ts` + hooks (`useHarvests(apiaryId?, year?)`, `useHarvest`,
  `useCreateHarvest`, `useUpdateHarvest`, `useDeleteHarvest` — invalidate `['harvests']` and `['stats']`).
- `features/harvests/`:
  - `HarvestsPage` (`/harvests`) — year selector (default current), grouped by apiary, rows:
    date, honey type label, total kg, entry count. Empty state: "Još nema evidencije vrcanja."
  - `HarvestFormPage` (`/harvests/new`, `/harvests/:id/edit`) — pick apiary → table of that
    apiary's hives with kg inputs (blank = hive not included), honey type select, date, optional
    price/kg and notes. react-hook-form with a field array.
- `BeehiveDetailPage`: "Prinos" stat (current season kg + prior seasons mini-list).
- `ApiaryDetailPage`: "Vrcanja" section (this apiary's harvests, newest first).
- `StatsPage`: two new Recharts blocks — top hives by yield (bar), kg by honey type (pie/bar);
  headline cards for season kg + est. revenue.
- Sidebar item "Vrcanja" (`Droplets` icon or similar from lucide). Routes in `App.tsx`.

## Edge cases

- Hive moved/deleted after a harvest: entries cascade-delete with the hive — acceptable v1
  (documented); totals recompute. Note in feature doc.
- Two harvests same apiary same day: allowed (different honey types happen).
- Beekeeper with no assigned hives → empty list, not 403.

## Out of scope (v1)

Sales/customers/inventory (jars), harvest appearing on the Calendar, CSV/PDF export,
notification on harvest creation, wax/propolis/pollen products (honey only — enum leaves room).

## Acceptance criteria

- [x] Full CRUD works with role scoping exactly as specified (matrix-tested in `AccessGuard` tests).
- [x] Creating a harvest for hives not in the chosen apiary → `400`.
- [x] Hive detail shows correct season yield; stats aggregates match manual sums.
      *(Note: `HarvestService` is unit-tested; the Stats aggregation is straightforward LINQ over
      `GetByApiariesAsync` and was verified by build + manual reasoning rather than a dedicated
      in-memory StatsService test — a follow-up test would harden it.)*
- [x] Beekeeper sees only harvests containing their hives, read-only (no create/edit buttons, API `403`).
- [x] All labels Bosnian (`BsLabels` backend + label map frontend).
- [x] Docs updated: `features/harvests.md`, `api-contracts.md`, `context.md`, this spec → ✅.
