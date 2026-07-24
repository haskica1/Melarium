# SPEC-08 — Treatment Log ("Evidencija tretmana")

| | |
|---|---|
| **Status** | ✅ Implemented (2026-07-03) |
| **Effort** | M (~1–2 days) |
| **Depends on** | nothing (SPEC-02 and SPEC-04 consume it — both degrade gracefully) |
| **New secrets / packages** | none (PDF via existing jsPDF; one embedded TTF asset, see below) |

## Goal

Beekeepers are legally required to keep a record of veterinary medicinal products applied to their
colonies (EU Reg. 2019/6 čl. 108 / BiH veterinarski propisi): date, product, dose, supplier,
withdrawal period, treated hives — kept for 5 years and shown to the veterinary inspector on
request. Today the app has nothing for this. This spec adds a **treatment register** (primarily
varroa — Apivar, oksalna kiselina… — but also nozema etc.) with a printable **PDF register per
apiary/year**, which is the actual legal deliverable.

## User stories

- As an OrganizationAdmin/ApiaryAdmin, after applying strips I record one treatment for the apiary:
  product, dose, date, LOT number, which hives (default: all).
- As a user, on a hive page I see its treatment status ("Tretman u toku" / "Karenca do 15.09.").
- As an admin, before an inspection visit I download "Evidencija tretmana — {pčelinjak}, {godina}"
  as PDF and hand it over.

## Domain rules

- A treatment is an **apiary-scoped event** covering a selected set of that apiary's hives
  (form defaults to *all* hives — varroa treatment is normally applied to the whole apiary).
- **Karenca (withdrawal):** `karencaUntil = (endDate ?? startDate) + withdrawalDays`.
  Many registered bee products have karenca 0 — the register must still show the value.
- **Status derivation** (computed, not stored): `endDate == null` → **U toku** (strips still in);
  else `today ≤ karencaUntil` → **Karenca**; else **Završen**.
- Register prints **chronologically ascending**; year filtering uses `startDate`'s year.
- Deleting a treatment is for mistakes only (5-year legal retention is the user's responsibility —
  the delete confirm dialog says so).

## Backend

### Entities

```
Treatment : BaseEntity                    // one application event, apiary-scoped
  ApiaryId         int      (FK)
  Purpose          TreatmentPurpose enum { Varoa=1, Nozema=2, KrecnoLeglo=3, Ostalo=99 }
  ProductName      string(100)           // "Apivar", "Oksalna kiselina 3.2%"
  ActiveSubstance  ActiveSubstance enum { Amitraz=1, Flumetrin=2, TauFluvalinat=3, Kumafos=4,
                                          OksalnaKiselina=5, MravljaKiselina=6, Timol=7, Ostalo=99 }
  Method           ApplicationMethod enum { Trake=1, Nakapavanje=2, Sublimacija=3, Isparavanje=4,
                                            UPrihrani=5, Prskanje=6, Ostalo=99 }
  DosePerHive      string(100)           // "2 trake po košnici", "5 ml po ulici pčela"
  StartDate        DateTime
  EndDate          DateTime?             // strips removed / application finished
  WithdrawalDays   int                   // karenca in days, 0 = nema
  BatchNumber      string(50)?           // LOT — legally expected, encourage in the form
  Supplier         string(100)?          // where purchased
  Notes            string(500)?
  CreatedById      int?  (FK User, SET NULL)
  Entries          ICollection<TreatmentEntry>

TreatmentEntry : BaseEntity               // per-hive line
  TreatmentId  int   (FK, cascade delete)
  BeehiveId    int   (FK, cascade delete)
  DoseNote     string(100)?              // only when deviating from DosePerHive
```

Repository `ITreatmentRepository` (+ `IUnitOfWork` property): `GetByOrganizationAsync(orgId, year?)`,
`GetByApiaryAsync(apiaryId, year?)`, `GetByBeehiveAsync(beehiveId)`, `GetWithEntriesAsync(id)`,
`GetActiveForBeehivesAsync(beehiveIds, asOf)` (projection: hiveId + karencaUntil + status — used by
hive detail badge, SPEC-02 warning, SPEC-01 context). Migration `AddTreatments`.

### Validation & business rules

- `entries` non-empty; every `beehiveId` must belong to `apiaryId`; no duplicate hive per treatment.
- `productName` and `dosePerHive` required (legal fields); `startDate` not in the future
  (tolerance +1 day); `endDate ≥ startDate`; `withdrawalDays` 0–365.
- Overlapping treatments on the same apiary/hive are **allowed** (combined treatments happen).
- Update replaces the entry set (delete + recreate inside one `SaveChangesAsync` — same semantics
  as SPEC-02 harvests).

### Endpoints (`TreatmentsController`, `/api/treatments`)

| Method | Path | Body → Returns |
|---|---|---|
| GET | `/treatments?apiaryId=&beehiveId=&year=` | → `TreatmentDto[]` (role-scoped; incl. computed `karencaUntil`, `status`, `hiveCount`) |
| GET | `/treatments/{id}` | → `TreatmentDetailDto` (entries with hive names, batch, supplier, createdByName) |
| POST | `/treatments` | `{ apiaryId, purpose, productName, activeSubstance, method, dosePerHive, startDate, endDate?, withdrawalDays, batchNumber?, supplier?, notes?, entries: [{beehiveId, doseNote?}] }` → `201` |
| PUT | `/treatments/{id}` | same shape → detail DTO |
| DELETE | `/treatments/{id}` | → `204` |

Computed DTO fields (`karencaUntil`, `status`) → manual mapping, per the Diets precedent.

**Authorization (via `IAccessGuard`, apiary-scoped — identical matrix to SPEC-02):**
SystemAdmin/OrganizationAdmin: all org apiaries. ApiaryAdmin: own apiary only.
**Beekeeper: read-only**, only treatments containing at least one of their assigned hives
(whole treatment visible — filtering rows would falsify the record).

## Frontend

- Models + `treatmentService.ts` + hooks (`useTreatments(filters)`, `useTreatment`, create/update/
  delete mutations — invalidate `['treatments']`).
- `features/treatments/`:
  - `TreatmentsPage` (`/treatments`) — year selector (default current), grouped by apiary, rows:
    start date, product, purpose label, status badge, hive count. Empty state: "Još nema evidencije
    tretmana." Per-apiary button **"Preuzmi evidenciju (PDF)"**.
  - `TreatmentFormPage` (`/treatments/new`, `/treatments/:id/edit`) — pick apiary → checkbox table
    of its hives (**all pre-checked**, optional per-hive `doseNote`), product fields, dates,
    karenca, LOT, supplier. react-hook-form.
  - **Product presets** — one static const `TREATMENT_PRESETS` (~6 common registered products:
    Apivar/amitraz/trake, Bayvarol/flumetrin/trake, Apiguard/timol/isparavanje, oksalna
    nakapavanje, oksalna sublimacija, mravlja isparavanje) that pre-fills substance/method/dose/
    karenca on selection; everything stays editable. Data quality for free, zero backend.
- `BeehiveDetailPage`: compact "Tretmani" line/card — latest treatment + status badge
  ("Tretman u toku" warning tint / "Karenca do {date}" / date + product when done), link to the
  hive's history (`/treatments?beehiveId=`).
- `ApiaryDetailPage`: "Tretmani" section, newest first.
- Sidebar item "Tretmani" (lucide `Pill` or similar). Routes in `App.tsx`.
- All enum labels Bosnian (backend `BsLabels` + frontend label maps): namjena, aktivna tvar
  ("Oksalna kiselina"…), način primjene ("Nakapavanje", "Sublimacija"…).

### PDF register (the legal artifact)

Client-side, existing **jsPDF** (same approach as `qrPdf.ts`), new util
`shared/utils/treatmentPdf.ts` — **no new npm package**:

- A4 landscape. Header block: organization, apiary, year, owner (current user name), generation
  date. Footer: page numbers.
- Columns (per legal requirements): # · Početak · Kraj · Preparat · Aktivna tvar · Namjena ·
  Način primjene · Doza · Košnice (count + names, wrapped small) · LOT · Dobavljač ·
  Karenca (dana) · Karenca ističe.
- Rows chronologically ascending; simple fixed-row-height pagination.
- **Font:** jsPDF's built-in fonts lack **č/ć/đ** (cp1252) — embed one open-license TTF with BCS
  glyphs (e.g., DejaVu Sans as base64 module `shared/utils/pdfFont.ts`, via `addFileToVFS`/
  `addFont`). A legal document with garbled diacritics is not acceptable; the asset is reusable
  for future PDFs.
- Filename: `evidencija-tretmana-{pčelinjak}-{godina}.pdf`.

## Integrations (soft — nothing here blocks on other specs)

- **SPEC-02 Harvests (whichever ships second):** harvest form shows a non-blocking warning banner
  when the harvest date falls inside a karenca window for any selected hive (client-side check
  against `useTreatments(apiaryId, year)` — no new endpoint). Warn, don't block: the beekeeper
  knows the product.
- **SPEC-04 alerts (only if 08 shipped):** two extra rules following the existing rule table —
  `StripsLeftIn` (Method = Trake, `EndDate == null`, `StartDate` > `Alerts:StripRemovalDays`
  (default 42) days ago → "Trake u košnicama pčelinjaka '{name}' su unutra {n} dana — vrijeme je
  za uklanjanje.") and `KarencaEnded` (karencaUntil passed within last scan window → "Istekla
  karenca za pčelinjak '{name}' — med se ponovo smije vrcati."). Same dedup mechanism.
- **SPEC-01 advisor context:** one line per hive — "Zadnji tretman: {product} ({substance}),
  {startDate}, status: {U toku|Karenca do X|Završen}" via `GetActiveForBeehivesAsync` / latest record.

## Edge cases

- Hive deleted after a treatment: entries cascade-delete with the hive — acceptable v1, same
  documented trade-off as harvests (the printed/archived PDF is the durable record).
- Winter oxalic treatment in December with karenca into January: belongs to the **start year**
  (year filter by `startDate`) — document in the feature file.
- Treatment with zero remaining hives after deletions → still listed (header data is the legal
  record); PDF prints hive count 0 with a dash.
- Beekeeper with no assigned hives → empty list, not 403.
- `endDate` set but `withdrawalDays = 0` → status jumps U toku → Završen (no Karenca phase) — fine.

## Out of scope (v1)

Efficacy tracking (mite-drop counts before/after), inspection-linked varroa monitoring, medicine
stock/inventory, seasonal "time to treat" reminders (SPEC-04 territory), notifiable-disease flow
(američka gnjiloća — prijava nadležnoj službi), immutable audit trail / e-signature, CSV export,
Calendar appearance.

## Acceptance criteria

- [x] Full CRUD works with role scoping exactly as specified (matrix-tested in `AccessGuard` tests).
- [x] Creating a treatment with hives not in the chosen apiary → `400`; duplicate hive rows → `400`.
- [x] `karencaUntil`/`status` computed correctly incl. `endDate == null` and `withdrawalDays = 0`
      cases (unit tests on the service/mapping).
- [x] Beekeeper sees only treatments containing their hives, read-only (no create/edit buttons, API `403`).
- [x] PDF register renders all columns listed above, paginates at 30+ rows, and prints č/ć/đ/š/ž
      correctly (embedded font).
- [x] Hive detail shows the correct status badge for an active-karenca hive.
- [x] All labels Bosnian (`BsLabels` backend + label map frontend).
- [x] Docs updated: `features/treatments.md`, `api-contracts.md`, `context.md`, glossary
      ("karenca", "LOT broj"), this spec → ✅.
