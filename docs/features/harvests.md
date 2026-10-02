# Feature: Harvests (Prinosi — med i ostali pčelinji proizvodi)

## Overview

Records every bee product an organization collects: honey extractions (vrcanja, SPEC-02) and, since
SPEC-30, comb honey, wax, propolis, pollen, royal jelly, bee bread and venom — per hive, as one figure
for an apiary, or as one figure for the whole organization. It is the income side next to Expenses and
feeds the Stats page, the dashboard yield card and the season report (SPEC-25).
Specs: [SPEC-02](../specs/SPEC-02-harvests.md), [SPEC-30](../specs/SPEC-30-hive-products.md);
decision: ADR-049.

SPEC-30 first shipped as a separate module (2026-09-30) and was folded into this one the next day,
before anything was committed — the two had the same shape. There is one table, one page, one form.

## Domain Rules

- A **Harvest** is one collection of one product (`ProductType`, `HiveProductType`) on one date.
- **Three levels, exactly one per record:**

  | Level | `ApiaryId` | Quantity | Written by |
  |---|---|---|---|
  | Per hive (*po košnicama*) | set | `HarvestEntry` lines | apiary managers |
  | One figure for the apiary (*ukupno za pčelinjak*) | set | `BulkKg` | apiary managers |
  | Whole organization (*cijela organizacija*, zajednički) | `NULL` | `BulkKg` | **OrganizationAdmin only** |

  No mixed records ("part per hive, the rest as one figure") — two records instead. `ApiaryId = NULL`
  means the organization's own record (e.g. wax rendered from every apiary's combs), the meaning
  `Expense.ApiaryId = NULL` has had since SPEC-25 — never "unknown". The row therefore carries its own
  `OrganizationId`.
- Totals are **derived, never stored** (`Domain/Common/HarvestTotals`): `TotalKg = BulkKg ?? Σ
  Entries.QuantityKg`; `EstimatedRevenue = TotalKg × PricePerKg`, `null` without a price.
- Every entry's `beehiveId` must belong to the record's apiary; a hive may appear at most once
  (duplicate → validator 400, foreign hive → service 400 with the reason under `errors.detail`).
- The **apiary is immutable** after creation; a record of the organization stays one. Update
  **replaces** the entry set and may switch between per-hive and one figure (not for an organization
  record, which has no hives to split over).
- `HoneyType` (Bagrem, Lipa, … via `BsLabels`) is **required for honey and dropped for every other
  product**; so is `FramesExtracted`.
- Deleting a beehive cascades to its lines; deleting an apiary cascades to its records — it does
  **not** turn them into organization records.

### Products and units

| Product | Enum | Entered and shown in | Price in the UI |
|---|---|---|---|
| Med | `Honey = 1` | kg | KM/kg |
| Med u saću | `CombHoney = 2` | kg | KM/kg |
| Vosak | `Wax = 3` | kg | KM/kg |
| Propolis | `Propolis = 4` | g | KM/kg |
| Polen | `Pollen = 5` | kg | KM/kg |
| Matična mliječ | `RoyalJelly = 6` | g | KM/g |
| Perga | `BeeBread = 7` | kg | KM/kg |
| Apitoksin | `BeeVenom = 8` | g (to 1 mg) | KM/g |
| Ostalo | `Other = 99` | kg | KM/kg |

The database is always kg (`numeric(12,6)` — one milligram) and KM/kg (`numeric(8,2)`). The display
unit lives in **one** module, `shared/utils/hiveProductUnits.ts`, used by the screens and by both
exports, so a product cannot read "350 g" in one place and "0,35 kg" in another. Changing the product
in the form **converts** what was typed (350 g of propolis does not become 350 kg of wax).

### Honey is honey, and kg never add up across products

- **Comb honey is its own product and never counts as honey** (Asim's decision): it sells differently
  and would distort the honey yield and the per-hive average.
- **No list, card, stat or report sums kg across products** — 200 g of royal jelly beside 20 kg of wax
  makes no total, and on a shared chart axis the jelly vanishes. Revenue (KM) is the only cross-product sum.
- Enforced by the compiler: every aggregate method of `IHarvestRepository` takes a required
  `HarvestKind` (`Honey`, `OtherProducts`, or `All` — lists only, never sums), so no consumer can sum
  "all harvests" and silently add wax to the honey yield.

## Access (`IAccessGuard`, apiary-scoped — same matrix as apiary management)

| Role | Apiary records | Organization records |
|---|---|---|
| OrganizationAdmin | read + write, every apiary of the org | read + write |
| ApiaryAdmin | read + write, own apiary | **read only** |
| Beekeeper | **read only**, records containing an assigned hive | never |
| SystemAdmin | with an `apiaryId` filter | — (no organization) |

- A Beekeeper sees the **whole** record once one of their hives is in it (filtering lines would make
  `totalKg` lie). A one-figure record has no hives, so a Beekeeper never sees it.
- **Downgrade lock (SPEC-24)** is applied by hand to the org list **and** the Beekeeper list: records of
  a locked apiary are left out; a Beekeeper assigned only to locked hives sees nothing.

## Plan

**Honey is on every plan**, as it has been since SPEC-02. Writing (POST, PUT) any other product needs
**Standard, Pro or Max** (`PlanFeature.HiveProducts = 6` → `402 plan-limit`). Reading and deleting never
do: a Free organization — including one whose trial ended — keeps seeing its products on the page, the
hive card, the stats and the report, and may delete them, because a past season's report must not
silently lose part of its revenue. On update both the stored and the new product are checked, so Free
can neither edit its earlier wax nor turn honey into wax (or wax into honey). The UI mirrors this with
`isFeatureLocked(plan, 'hiveProducts')`: non-honey options disabled in the form, no "Uredi" on a
non-honey card, and a notice above the list linking to `/plans`.

`PlanFeature` 5 is deliberately skipped — it belongs to Achievements (SPEC-27, still in a stash).

## API (`/api/harvests`)

- `GET /harvests?apiaryId=&beehiveId=&year=&allProducts=&productType=` — role-scoped list. **Honey only
  by default**, so a client older than SPEC-30 never meets wax; `allProducts=true` lists everything,
  `productType` one product. Each item: `productType`/`productTypeName`, `honeyType?`, `bulkKg?`,
  `totalKg`, `entryCount`, `apiaryId?`/`apiaryName`, `estimatedRevenue`.
- `GET /harvests/{id}` — detail with per-hive entries (empty for a one-figure record).
- `POST /harvests` — `{ apiaryId?, date, productType?, honeyType?, pricePerKg?, bulkKg?, notes?,
  entries:[{beehiveId, quantityKg, framesExtracted?}] }` → 201. No `productType` = honey.
- `PUT /harvests/{id}` — same minus `apiaryId`; no `productType` = keep the record's own.
- `DELETE /harvests/{id}` → 204, on every plan.
- `GET /harvests/hive/{beehiveId}/yield` — honey only, `{ currentSeasonKg, byYear:[{year, kg}] }` (kept
  for older clients).
- `GET /harvests/hive/{beehiveId}/summary` — every product per year from this hive's own lines, for the
  hive card. Contract details in `api-contracts.md`.

## Consumers

| Where | What it sums |
|---|---|
| Stats (`GET /api/stats`) | Every product side by side: `harvestsByProduct[]` (honey first). Honey: `seasonTotalKg`, `estimatedRevenue`, `kgByApiary[]` (organization records as a last "Zajedničko" row), `kgByHoneyType[]`, `kgByPasture[]` (own "Zajedničko" bucket), `yearlyYield[]`; `topHivesByYield[]` from per-hive lines only. Other products (current year): `hiveProductsByApiary[]`, `hiveProductsByPasture[]`, `hiveProductsByBeehive[]` |
| Season report (SPEC-25) | One "Prinosi" section: `harvests.byProduct` overview (revenue the only total), honey in detail (one-figure records in every total but the per-hive table), the other products per apiary, pasture and hive; balance = honey + products − expenses |
| Dashboard yield card (SPEC-29) | Honey; an apiary's one figure counts for its managers, an organization record for the OrganizationAdmin only, neither for a Beekeeper |
| Weekly AI summary | Honey, organization records included |
| AI Asistent | Honey per year for a hive |

## UI

- Sidebar item **"Prinosi"** (`Droplets` icon), all roles but SystemAdmin.
- `HarvestsPage` (`/harvests`) — product filter ("Svi proizvodi" + each product) and year; honey
  vitals (Ukupno meda, Vrcanja, Procj. prihod, Najviše) when the filter is Med, otherwise product
  vitals with no kg total (Zapisa, Proizvoda, Procj. prihod, Bez cijene); chips per product; records
  grouped by apiary with "Zajedničko — cijela organizacija" first; `?beehiveId=` filter from the hive card.
- `HarvestFormPage` (`/harvests/new`, `/harvests/:id/edit`) — "Gdje je prikupljeno" (Po košnicama /
  Ukupno za pčelinjak / Cijela organizacija — the last for the owner only, and on edit the level stays
  on its side of the apiary line), apiary, product, honey type and the frames column for honey only,
  date, price in the product's unit, notes, and the non-blocking karenca warning (SPEC-08) for every
  product except at organization level.
- `BeehiveDetailPage` — `HiveYieldCard` "Prinos": honey kg for the season, other products as chips
  when there are any, prior seasons, link "Svi prinosi ove košnice".
- `ApiaryDetailPage` — `ApiaryHarvestsSection` "Prinosi" (every product of this apiary, newest first).
- `StatsPage` — one "Prinosi — sezona" section (`features/stats/HarvestStats.tsx`): a tile per product
  with revenue and what has no price, then "Med" (honey type pie, top hives, by year, by pasture), then
  "Ostali proizvodi" per apiary, pasture and hive (the hive table shows ten rows, the rest on request).
- `/reports` — the same "Prinosi" structure on screen, in the PDF and in Excel (see `season-report.md`).
- `/plans` — row "Vosak, propolis i ostali pčelinji proizvodi".

## Tests

`HarvestServiceTests` (honey on Free, other products gated, organization records, roles, Beekeeper,
lock, totals, hive yield honey-only, default list honey-only), `HarvestValidatorTests`,
`StatsServiceTests` (honey never includes other products, includes one-figure and organization
records), `ReportServiceTests` and `DashboardServiceTests` (one-figure honey everywhere but the
per-hive table; never for a Beekeeper).
