# Feature: Harvests (Vrcanja i prinosi)

## Overview

Records honey extractions (vrcanja): how many kg of which honey type were extracted, per apiary, per
date, broken down by hive. Adds the income side to complement Expenses, and feeds the Stats page with
yield/revenue aggregates. Implemented per [SPEC-02](../specs/SPEC-02-harvests.md).

## Domain Rules

- A **Harvest** is an apiary-scoped extraction event with one **HarvestEntry** per hive
  (`QuantityKg` > 0, optional `FramesExtracted`).
- Every entry's `beehiveId` must belong to the harvest's `apiaryId`; a hive may appear at most once
  per harvest (both enforced — duplicate in the validator → 400, foreign hive in the service → 400).
- The **apiary is immutable** after creation (entries belong to that apiary's hives); `UpdateHarvestDto`
  has no `apiaryId`. Update **replaces** the entry set (delete + recreate in one `SaveChangesAsync`).
- `HoneyType`: English enum names (`Acacia, Linden, Chestnut, Sunflower, Meadow, Forest, Rapeseed,
  Other`) with Bosnian labels via `BsLabels` (Bagrem, Lipa, Kesten, …). Mapping is **manual** in the
  service (computed-label + totals DTO, same policy as Queens/Diets).
- `PricePerKg` is optional; when set, `EstimatedRevenue` = `TotalKg × PricePerKg`.
- Deleting a beehive cascades to its harvest entries (documented v1 trade-off; totals recompute).

## API (`/api/harvests`)

- `GET /harvests?apiaryId=&year=` — role-scoped list; each item carries `totalKg`, `entryCount`,
  `apiaryName`, `honeyTypeName`, `estimatedRevenue`.
- `GET /harvests/{id}` — detail with per-hive entries (incl. hive names).
- `POST /harvests` — `{ apiaryId, date, honeyType, pricePerKg?, notes?, entries:[{beehiveId, quantityKg, framesExtracted?}] }` → 201.
- `PUT /harvests/{id}` — same shape minus `apiaryId` → detail.
- `DELETE /harvests/{id}` → 204.
- `GET /harvests/hive/{beehiveId}/yield` — `{ currentSeasonKg, byYear:[{year, kg}] }` for the hive
  detail "Prinos" card (access = viewing the hive).

## Access (`IAccessGuard`, apiary-scoped — same matrix as apiary management)

- **SystemAdmin / OrganizationAdmin**: all org apiaries. **ApiaryAdmin**: own apiary only.
- **Beekeeper: read-only**, and only harvests that contain at least one of their assigned hives
  (the whole harvest is visible — filtering rows would make `totalKg` lie).
- Create/Update/Delete call `EnsureCanManageApiaryAsync` (so Beekeepers can't write). The hive-yield
  endpoint uses `EnsureCanAccessBeehiveAsync`.

## Stats integration (`GET /api/stats`, org-scoped as before)

Added fields: `seasonTotalKg`, `estimatedRevenue`, `kgByApiary[]`, `kgByHoneyType[]`,
`topHivesByYield[]` (top 5, current year), `yearlyYield[]` (last 3 years). kg values use the new
`NameDecimalDto` (decimal). Aggregation reuses `IHarvestRepository.GetByApiariesAsync`.

## UI

- Sidebar item **"Vrcanja"** (`Droplets` icon), visible to all authenticated users.
- `HarvestsPage` (`/harvests`) — year selector, vitals, harvests grouped by apiary; managers get
  create/edit/delete, Beekeepers read-only.
- `HarvestFormPage` (`/harvests/new`, `/harvests/:id/edit`) — pick apiary → per-hive kg/frames rows
  (blank = hive not included), honey type, date, optional price/kg + notes. Apiary select is disabled
  in edit mode.
- `BeehiveDetailPage`: `HiveYieldCard` in the sidebar (current season kg + prior seasons).
- `ApiaryDetailPage`: `ApiaryHarvestsSection` (this apiary's harvests, newest first).
- `StatsPage`: "Prinosi meda" section — headline season kg + revenue, kg-by-honey-type pie,
  top-hives-by-yield bar, yearly-yield bar.

## Tests

`HarvestServiceTests` — foreign-hive create → `ValidationException` (400, nothing saved); Beekeeper
list filtered to harvests containing assigned hives; Beekeeper with no assignments → empty.
