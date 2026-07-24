# Feature: Treatments (Evidencija tretmana)

## Overview

The legally required register of veterinary medicinal products applied to colonies (EU Reg. 2019/6
čl. 108 / BiH veterinarski propisi): preparation, dose, dates, LOT number, supplier, withdrawal
period (karenca), treated hives — with a printable **PDF register per apiary/year** as the artifact
handed to the veterinary inspector. Primarily varroa treatments (Apivar, oksalna kiselina…), but
also nozema etc. Implemented per [SPEC-08](../specs/SPEC-08-treatments.md).

## Domain Rules

- A **Treatment** is an apiary-scoped application event with one **TreatmentEntry** per treated hive
  (optional `DoseNote` when a hive deviates from `DosePerHive`). The form defaults to *all* hives.
- Every entry's `beehiveId` must belong to the treatment's `apiaryId`; a hive appears at most once
  (duplicate in the validator → 400, foreign hive in the service → 400). Entries must be non-empty.
- The **apiary is immutable** after creation; `UpdateTreatmentDto` has no `apiaryId`. Update
  **replaces** the entry set (same semantics as Harvests).
- **Karenca (withdrawal):** `karencaUntil = (endDate ?? startDate) + withdrawalDays`. Many registered
  bee products have karenca 0 — the register still shows the value.
- **Status is computed, never stored** (`TreatmentStatusHelper`, single source of truth):
  `endDate == null` → **U toku**; else `today ≤ karencaUntil` → **Karenca**; else **Završen**.
  `endDate` set with `withdrawalDays = 0` jumps U toku → Završen (no Karenca phase).
- Year filtering uses `startDate`'s year (a December oxalic treatment with karenca into January
  belongs to the start year). The PDF register prints chronologically **ascending**.
- Enums: `TreatmentPurpose` (Varroa, Nosema, Chalkbrood, Other), `ActiveSubstance` (Amitraz,
  Flumethrin, TauFluvalinate, Coumaphos, OxalicAcid, FormicAcid, Thymol, Other), `ApplicationMethod`
  (Strips, Trickling, Sublimation, Evaporation, InFeed, Spraying, Other) — English enum names,
  Bosnian labels via `BsLabels` (manual DTO mapping, computed `karencaUntil`/`status`, Diets precedent).
- Deleting a treatment is for mistakes only — the confirm dialog states the 5-year legal retention
  duty. Deleting a hive cascades to its treatment entries (documented v1 trade-off; the printed PDF
  is the durable record). A treatment left with zero entries stays listed (header data is the record).
- `startDate` not in the future (+1 day tolerance); `endDate ≥ startDate`; `withdrawalDays` 0–365;
  overlapping treatments on the same apiary/hive are **allowed** (combined treatments happen).

## API (`/api/treatments`)

- `GET /treatments?apiaryId=&beehiveId=&year=` — role-scoped list; each item carries computed
  `karencaUntil`, `status`/`statusName`, `hiveCount`, `hiveNames`, Bosnian `*Name` labels.
- `GET /treatments/{id}` — detail with per-hive entries (hive names, doseNote) + `createdByName`.
- `POST /treatments` — `{ apiaryId, purpose, productName, activeSubstance, method, dosePerHive,
  startDate, endDate?, withdrawalDays, batchNumber?, supplier?, notes?, entries:[{beehiveId, doseNote?}] }` → 201.
- `PUT /treatments/{id}` — same shape minus `apiaryId` → detail.
- `DELETE /treatments/{id}` → 204.

## Access (`IAccessGuard`, apiary-scoped — same matrix as Harvests)

- **SystemAdmin / OrganizationAdmin**: all org apiaries. **ApiaryAdmin**: own apiary only.
- **Beekeeper: read-only**, only treatments containing at least one assigned hive; the whole
  treatment is visible (filtering rows would falsify the legal record). No assigned hives → empty
  list, not 403.

## PDF register (the legal artifact)

Client-side jsPDF (`shared/utils/treatmentPdf.ts`), A4 landscape, per apiary/year, filename
`evidencija-tretmana-{pčelinjak}-{godina}.pdf`. Header: organization, apiary, year, owner,
generation date; footer: page numbers; fixed-row pagination. Columns: # · Početak · Kraj · Preparat ·
Aktivna tvar · Namjena · Način · Doza · Košnice · LOT · Dobavljač · Karenca (dana) · Karenca ističe.
jsPDF built-in fonts are cp1252 (no č/ć/đ) → **DejaVu Sans embedded** as a lazy-loaded base64 module
(`shared/utils/pdfFont.ts`, ~1 MB chunk, reusable for future PDFs).

## Integrations (all soft)

- **Harvest form (SPEC-02):** non-blocking amber banner when the harvest date falls inside a
  treatment/karenca window for any hive with a quantity entered (client-side check via
  `useTreatments(apiaryId)`).
- **Alerts (SPEC-04):** two rules in `AlertRuleService` — `StripsLeftIn` (15): strips (`Trake`) with
  no `endDate` in for ≥ `Alerts:StripRemovalDays` (42) days; `KarencaEnded` (16): karenca expired
  within the last 3 days. Both apiary-level recipients, `relatedEntityType = Treatment`, 7-day dedup.
- **Advisor context (SPEC-01):** one line per hive — "Zadnji tretman: {product} ({substance}),
  {date}, status: …" via `ITreatmentRepository.GetLatestForBeehivesAsync` (`TreatmentLatestInfo`).

## UI

- Nav item **"Tretmani"** (`Pill` icon), visible to all authenticated users.
- `TreatmentsPage` (`/treatments`) — year selector, vitals, treatments grouped by apiary, status
  badges (U toku / Karenca do … / Završen), per-apiary **"Preuzmi evidenciju (PDF)"** button;
  honors `?beehiveId=` for a single hive's history. Managers get create/edit/delete; Beekeepers read-only.
- `TreatmentFormPage` (`/treatments/new`, `/treatments/:id/edit`) — **product presets**
  (`TREATMENT_PRESETS`: Apivar, Bayvarol, Apiguard, oksalna ×2, mravlja — pre-fill substance/method/
  dose/karenca, all editable), apiary → hive checkbox table (**all pre-checked**, per-hive dose
  deviation), dates, karenca, LOT, supplier. Apiary select disabled in edit mode.
- `BeehiveDetailPage`: `HiveTreatmentCard` in the sidebar — latest treatment + status badge, link to
  the hive's history.
- `ApiaryDetailPage`: `ApiaryTreatmentsSection` (newest first, opens by default when a treatment is
  active/in karenca).

## Tests

`TreatmentStatusHelperTests` — status/karencaUntil derivation incl. `endDate == null`, zero-karenca,
and boundary days. `TreatmentServiceTests` — foreign-hive create → `ValidationException` (nothing
saved); duplicate hive → 400 (validator); Beekeeper list filtered to treatments containing assigned
hives, read-only; Beekeeper with no assignments → empty. `AdvisorContextBuilderTests` — treatment
line rendering (U toku + Karenca do …).
