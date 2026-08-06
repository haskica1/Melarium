# SPEC-12 — Prehrana na nivou pčelinjaka ("Apiary-level feeding programmes")

| | |
|---|---|
| **Status** | 📋 Planned |
| **Effort** | L (~5 days) — one live-data migration, six downstream consumers, two auth changes, one alert rule, cost attribution |
| **Depends on** | nothing new; touches SPEC-01 (advisor), SPEC-04 (weekly summary + alerts), SPEC-11 (calendar/ICS), SPEC-14 (in-app help), and the Expenses module (Phase E) |
| **New secrets / packages** | none |
| **Breaking** | **Yes** — `Diet.BeehiveId` is replaced, and Beekeepers lose diet-create rights. Backend + frontend must deploy together. |
| **Route prefix** | stays **`/api/feedings`** — no rename (see "A note on the route prefix") |

## Goal

Today a feeding programme (`Diet`) belongs to **exactly one beehive**. Feeding an apiary of 20 hives
means creating the programme once and copying it 19 times (`POST /feedings/{id}/copy`), which produces
20 independent rows that then have to be ticked, edited and stopped 20 times over.

This spec inverts the model to match how treatments already work and how beekeepers actually work:
**create the programme for the apiary, then choose which hives it applies to.** A hive then shows a
clear "ima aktivnu prehranu" indicator, and the programme is managed in one place.

### Why this is worth doing beyond convenience

The copy mechanism does not just create extra work — it multiplies data across the app:

| Consumer | Today, for one programme copied to 20 hives |
|---|---|
| `CalendarObligationService` | 20 obligations **per feeding date** (`feeding-{entryId}`), all identical |
| ICS feed / Google-MS sync (SPEC-11) | those same 20 duplicates land in the user's real calendar |
| `CalendarService` | 20 rows per date on the in-app calendar |
| `StatsService` | "Aktivne prehrane: 20" for what the beekeeper thinks of as one programme |
| Weekly summary (SPEC-04) | feeding counts inflated by the copy factor |

After this change, one programme = one set of obligations, whatever the hive count. The calendar
duplication is the single strongest argument for the change and should be verified explicitly
(see acceptance criteria).

### A note on the route prefix

`docs/api-contracts.md` and `docs/context.md` document these endpoints as `/api/diets`. **That is
stale documentation.** The controller is `[Route("api/feedings")]`
([DietsController.cs:14](backend/Melarium.API/Controllers/DietsController.cs:14)), `dietService.ts`
calls `/feedings/*`, the React routes are `/feedings/*`, and `helpRoutes.ts` registers `/feedings/*`.
This spec keeps `/api/feedings` — renaming would break the frontend, the help registry and any
bookmarked URL for zero benefit. Fixing the two stale docs is part of the deliverable.

The internal names stay `Diet` / `DietService` / `DietsController` (entity vocabulary), while the
route and all user-facing strings stay "prehrana". That split already exists; this spec does not
change it.

## User stories

- As an OrganizationAdmin/ApiaryAdmin, I create one feeding programme for a pčelinjak — food type,
  amount per hive, start date, duration, frequency — and tick which hives are on it (default: all).
- As a beekeeper standing at the apiary, I tick "Hranjenje obavljeno" **once** per round, not once
  per hive.
- As any user opening a hive, I immediately see **"Aktivna prehrana"** with what it is and when the
  next feeding is due.
- As an admin standing at one hive that needs feeding, I start a programme **from that hive's page**
  and only that hive is pre-ticked — I do not have to go find the apiary first.
- As an admin, a colony gets strong mid-programme and I remove just that hive from the programme
  without losing the record that it was on it until that date.
- As an admin, I open **Prehrana** in the sidebar and see every programme across my apiaries — the
  same top-level place Tretmani already has.

## Domain rules

### The model decision (read this first)

A diet has **two dimensions**: *which hives* and *which dates*. Treatments only have the first, so
`TreatmentEntry` (one row per hive) was enough there. For diets we must choose where the schedule
lives:

| | Rounds are diet-level | Rounds are per hive |
|---|---|---|
| Rows for 20 hives × 15 feedings | 20 + 15 | 300 |
| Checkboxes per feeding day | 1 | 20 |
| Can record "hive 7 was not fed on 12.09." | no (free-text note only) | yes |

**Chosen: rounds are diet-level.** Feeding is a single visit to the apiary with one canister — the
beekeeper feeds everything present, then leaves. Asking for 20 ticks per round would make the feature
worse than what exists today. The per-hive dimension that genuinely matters is *"is this hive on the
programme"*, not *"was this hive fed on this specific day"*.

The escape hatch for the rare exception is a free-text `Note` on the round
("košnice 7 i 12 preskočene — slabe"). If per-hive round tracking is ever really needed, it is an
additive change later (a `FeedingEntrySkip` table), not a rewrite.

**Consequence worth stating plainly:** `FeedingEntry` keeps its schedule semantics, its status enum
and its completion endpoint. The only change is one optional `Note` column. That keeps the migration
and the downstream consumers small.

### Rules

- A diet is **apiary-scoped** and covers a selected set of that apiary's hives via `DietBeehive`.
  The form defaults to **all** hives of the apiary (same default as treatments).
- A diet must have **at least one hive** at creation. It may end up with zero active hives later (all
  removed, or hives deleted) — it stays listed, like a treatment with zero remaining hives.
- `FeedingEntry` = one **feeding round** for the whole group. Generation is unchanged:
  `count = DurationDays / FrequencyDays` (min 1), dates `StartDate + i × FrequencyDays`.
- `DietStatus` stays **diet-level** and its transitions are unchanged:
  `NotStarted → InProgress → Completed | StoppedEarly`.
- **A hive has an active feeding** when: a `DietBeehive` row links it to the diet, `RemovedOn == null`,
  and the diet's status is `NotStarted` or `InProgress`. This is the single definition — the badge,
  the advisor context, the calendar filter and any future alert rule must all use it, not re-derive
  their own.
- **"On the programme" always means `RemovedOn == null`.** `hiveCount` on every DTO counts active
  links only; removed hives appear in the detail view as history, never in counts.
- Hives may be **added to a running diet**. They join the schedule from that point on; past rounds
  are not retroactively attributed to them (`DietBeehive.CreatedAt` records when they joined).
- Hives may be **removed from a running diet** — soft, via `RemovedOn`. Hard-deleting the link would
  silently erase the fact that the hive was fed at all that month.
- **A removed hive can be re-added.** That creates a *new* `DietBeehive` row; the old one stays as
  history with its `RemovedOn` date. This is why the uniqueness constraint is partial (see EF config)
  — a plain unique index on `(DietId, BeehiveId)` would make re-adding impossible.
- Overlapping diets on the same hive are **allowed** (a stimulative feeding can run alongside a
  protein supplement). Everything that reads "the active feeding" for a hive must therefore handle
  **a list, not a single value** — see `DietActiveInfo`.
- A hive can only be on a diet of **its own apiary**. Validation must reject cross-apiary hive ids —
  never trust the client's list.

## Backend

### Entities

```
Diet : BaseEntity                          // CHANGED: was beehive-scoped, now apiary-scoped
  ApiaryId        int   (FK, cascade)      // NEW — replaces BeehiveId
  Apiary          Apiary                   // NEW navigation (needed for apiaryName on DTOs)
  Name            string(200)
  StartDate       DateTime
  Reason          DietReason enum          // unchanged
  CustomReason    string(500)?
  DurationDays    int
  FrequencyDays   int
  FoodType        FoodType enum            // unchanged
  CustomFoodType  string(200)?
  AmountPerHive   decimal(6,2)?            // NEW — 1.00
  AmountUnit      FeedingAmountUnit?       // NEW — L | ml | kg | g
  AmountNote      string(100)?             // NEW — "1:1", "pola pogače" — what the number cannot carry
  Status          DietStatus enum          // unchanged
  EarlyCompletionComment string(1000)?
  CreatedById     int? (FK User, SET NULL)
  Beehives        ICollection<DietBeehive> // NEW
  FeedingEntries  ICollection<FeedingEntry>// unchanged
  ── REMOVED: BeehiveId, Beehive

DietBeehive : BaseEntity                   // NEW — which hives the programme covers
  DietId      int   (FK, cascade delete)
  Diet        Diet
  BeehiveId   int   (FK, cascade delete)
  Beehive     Beehive                      // for hive names on the detail DTO
  RemovedOn   DateTime?                    // null = still on the programme
  // "when added" = BaseEntity.CreatedAt — no extra column needed

FeedingEntry : BaseEntity                  // UNCHANGED except one new optional field
  DietId          int (FK, cascade)
  ScheduledDate   DateTime
  Status          FeedingEntryStatus
  CompletionDate  DateTime?
  Note            string(300)?             // NEW — "košnice 7 i 12 preskočene"
```

### Amount per hive — number + unit + note (decided 2026-07-30)

The programme records *what* and *how often* but not *how much*. Treatments solve this with a single
free-text `DosePerHive string(100)`. Feeding deliberately **does not copy that shape**, and the extra
field is the whole point:

- **The number is what makes consumption and cost possible at all.** `ExpenseItem` already stores
  `Quantity` + `Unit` + `UnitPrice` ("Šećer 25 kg @ 1.60/kg",
  [ExpenseItem.cs:7](backend/Melarium.Domain/Entities/ExpenseItem.cs:7)) — the price side already
  exists. The missing half is consumption, and `amount × active hives × rounds` supplies it. A string
  would make that permanently unreachable: migrating `"1 L sirupa 1:1"` back into a decimal on live
  data means parsing prose, which will not work reliably.
- **The note is what the number cannot carry.** "1 L sirupa **1:1**" and "1 L sirupa **2:1**" are the
  same litre and a completely different intervention — the first is spring stimulation, the second
  winter stores. Same for "pola pogače". Forcing that into a decimal loses beekeeping meaning; adding
  it as a separate short field loses nothing.

```
FeedingAmountUnit : Litre = 1, Millilitre = 2, Kilogram = 3, Gram = 4
```

New enum in `Melarium.Domain/Enums/`, with `BsLabels.Label(FeedingAmountUnit)` → `"L" | "ml" | "kg" | "g"`
and a matching label map in `frontend/src/core/models/index.ts` (the two are kept in sync by
convention — see the `BsLabels` class comment).

**All three fields are optional**, and the note is allowed **on its own**: someone who only wants to
write "pola pogače" must not be blocked by a required number. Programmes without a number simply
drop out of any future consumption estimate. Existing rows get `NULL` in the migration.

### EF configuration

- `DietConfiguration`: drop the `BeehiveId` index; add `HasOne(d => d.Apiary).WithMany()
  .HasForeignKey(d => d.ApiaryId).OnDelete(Cascade)`, `HasIndex(d => d.ApiaryId)`,
  `HasIndex(d => d.StartDate)` (mirrors `TreatmentConfiguration`), `Property(d => d.AmountPerHive)
  .HasPrecision(6, 2)` and `Property(d => d.AmountNote).HasMaxLength(100)`. `AmountUnit` is a plain
  nullable enum column — no configuration needed.
- `DietBeehiveConfiguration` (new): cascade on both FKs, index on `DietId` and on `BeehiveId`, and a
  **partial unique index** so a hive cannot be on the same programme twice *at the same time* while
  still allowing a re-add after removal:

  ```csharp
  builder.HasIndex(x => new { x.DietId, x.BeehiveId })
         .IsUnique()
         .HasFilter("\"RemovedOn\" IS NULL");
  ```

- `FeedingEntryConfiguration`: `Property(e => e.Note).HasMaxLength(300)`.
- `BeehiveConfiguration`: remove the `HasMany(b => b.Diets)` relationship
  ([BeehiveConfiguration.cs:50-54](backend/Melarium.Entity/Configurations/BeehiveConfiguration.cs:50))
  and the `Diets` navigation on `Beehive` (it no longer exists as a direct FK).

### Repository — `IDietRepository`

```csharp
Task<IEnumerable<Diet>> GetByApiaryAsync(int apiaryId, int? year = null);          // replaces GetByBeehiveIdAsync
Task<IEnumerable<Diet>> GetByOrganizationAsync(int organizationId, int? year = null); // NEW — /feedings page
Task<IEnumerable<Diet>> GetByBeehiveAsync(int beehiveId);                          // diets containing this hive
Task<IEnumerable<Diet>> GetByApiaryIdsAsync(IEnumerable<int> apiaryIds);           // replaces GetByBeehiveIdsAsync (calendar)
Task<Diet?> GetWithEntriesAsync(int id);                                           // + Beehives (incl. removed), with hive names
Task<Dictionary<int, List<DietActiveInfo>>> GetActiveForBeehivesAsync(IReadOnlyCollection<int> beehiveIds);
```

Naming deliberately mirrors `ITreatmentRepository` so the two feature slices read the same.

`DietActiveInfo` — lightweight read model in `Melarium.Domain/Common/`, mirroring
`TreatmentLatestInfo`:

```csharp
public record DietActiveInfo(
    int BeehiveId, int DietId, string DietName, FoodType FoodType, string? CustomFoodType,
    decimal? AmountPerHive, FeedingAmountUnit? AmountUnit, string? AmountNote,
    DateTime StartDate, DateTime? NextFeedingDate,
    int CompletedRounds, int TotalRounds);
```

**Returns a list per hive, not a single value**, because overlapping programmes are allowed by the
rules above. `NextFeedingDate` = earliest `Pending` round with `ScheduledDate >= today`, else the
earliest `Pending` round (an overdue one), else `null`.

All queries **filter in SQL**, not in memory (Phase 3 rule — `OrgManagementService` was the
cautionary tale). `GetActiveForBeehivesAsync` queries `DietBeehives` and projects, exactly as
`GetLatestForBeehivesAsync` queries `TreatmentEntries`.

### Validation & business rules

- `apiaryId` required and must exist; `beehiveIds` non-empty; every id must belong to `apiaryId`;
  no duplicates. Cross-apiary or duplicate ids → `400` with the same message shape
  `TreatmentService.EnsureEntriesBelongToApiaryAsync` uses.
- `startDate` required; `durationDays` > 0 and ≤ 365; `frequencyDays` > 0 and ≤ `durationDays`.
- `foodType` required; `customFoodType` required **when** `foodType == Custom` (currently not
  enforced — fix it here).
- `customReason` required when `reason == Custom` (same gap).
- `amountPerHive` optional; when present must be `> 0` and `≤ 100` (no hive gets a hectolitre) and
  `amountUnit` becomes **required**. `amountNote` optional, ≤ 100 chars, and valid **on its own**
  without a number. `note` on a round optional, ≤ 300 chars.
- `name` required, ≤ 200 chars.
- Update rules stay as they are: a `Completed`/`StoppedEarly` diet cannot be updated; a diet can only
  be **deleted** before it started and with no completed rounds.
- Changing the **hive set** is allowed on a running diet (that is the point) and is *not* blocked by
  the "no update after completion" rule — it goes through its own endpoints, not `PUT /feedings/{id}`.
- Adding hives is rejected on a `Completed` **or `StoppedEarly`** diet → `422`. Removing a hive is
  allowed in any status (it is a correction of the record).

### Endpoints (`DietsController`, `/api/feedings`)

| Method | Path | Body → Returns |
|---|---|---|
| GET | `/feedings?apiaryId=&beehiveId=&year=` | → `DietDto[]` (role-scoped; incl. `apiaryId`, `apiaryName`, `hiveCount`, `completedEntries`, `totalEntries`, `nextFeedingDate`) |
| GET | `/feedings/active?apiaryId=` | → `DietActiveInfoDto[]` — flat array, one row per (hive × active programme); the frontend groups by `beehiveId`. Hive badges for a whole apiary in one request |
| GET | `/feedings/{id}` | → `DietDetailDto` (rounds + `beehives[]` with names and `removedOn`) |
| POST | `/feedings` | `{ apiaryId, name, startDate, reason, customReason?, durationDays, frequencyDays, foodType, customFoodType?, amountPerHive?, amountUnit?, amountNote?, beehiveIds: int[] }` → `201` |
| PUT | `/feedings/{id}` | same shape **without** `beehiveIds` → detail DTO |
| DELETE | `/feedings/{id}` | → `204` |
| POST | `/feedings/{id}/beehives` | `{ beehiveIds: int[] }` — add hives to a running programme → detail DTO |
| DELETE | `/feedings/{id}/beehives/{beehiveId}` | soft-remove (sets `RemovedOn`) → detail DTO |
| POST | `/feedings/{id}/complete-early` | `{ comment }` → detail DTO |
| POST | `/feedings/{dietId}/feeding-entries/{entryId}/complete` | `{ note? }` → **`DietDetailDto`** |

Two contract details that are easy to get wrong:

- **`/feedings/active` must be declared before, or alongside, `/feedings/{id:int}`.** The existing
  `{id:int}` route constraint already prevents "active" from matching, but keep the literal route
  first anyway so the intent survives a future constraint change.
- **The round-completion endpoint gains a request body it does not have today.** `CompleteFeedingEntry`
  currently takes no body at all ([DietsController.cs:146](backend/Melarium.API/Controllers/DietsController.cs:146)).
  Adding `{ note? }` means a new `CompleteFeedingEntryDto` **plus** a `CompleteFeedingEntryValidator`
  (max 300) — `CLAUDE.md` requires FluentValidation on every endpoint, not inline checks — and
  `completeFeedingEntry` in `dietService.ts` must start sending a body. A missing/empty body must
  stay valid: ticking a round without a note is the normal case.
- **The round-completion endpoint keeps returning the full `DietDetailDto`**, as it does today
  ([DietsController.cs:146-151](backend/Melarium.API/Controllers/DietsController.cs:146)) — the tick
  also moves diet status and progress, so the detail is what the UI needs. Returning a bare
  `FeedingEntryDto` would be a silent frontend break for no gain. `IDietService` may still return
  `FeedingEntryDto` internally; the controller reloads the detail.

**Removed:** `GET /feedings/by-beehive/{beehiveId}` (→ `GET /feedings?beehiveId=`) and
`POST /feedings/{id}/copy` + `CopyDietDto` + `CopyDietValidator` + `CopyDietDialog.tsx`. Copying is
what this spec exists to eliminate; leaving it would create two ways to do the same thing badly.

### Authorization (via `IAccessGuard`)

Mirrors SPEC-08 treatments. There are **two** deviations from today's behaviour, not one — both
signed off on 2026-07-30:

| Action | SystemAdmin | OrgAdmin | ApiaryAdmin | Beekeeper |
|---|---|---|---|---|
| List / read | all | own org | own apiary | diets containing ≥1 assigned **active** hive |
| Create / update / delete | ✅ | own org | own apiary | ❌ 403 — **narrowed, see below** |
| Add / remove hives | ✅ | own org | own apiary | ❌ 403 |
| **Tick a feeding round** | ✅ | own org | own apiary | ✅ if assigned to ≥1 hive on the diet |
| Complete early | ✅ | own org | own apiary | ❌ 403 |

Writes use `EnsureCanManageApiaryAsync(diet.ApiaryId)`. Reads and round-ticking use a new check
"caller can access at least one active hive on this diet" built from `GetAssignedBeehiveIdsAsync()` —
the same shape as `TreatmentService.EnsureCanReadAsync`.

> **① This narrows Beekeeper rights (regression for existing users) — DECIDED: accept.** Today a
> Beekeeper assigned to a hive can **create, edit, delete and stop** that hive's feeding programme:
> `DietService` gates all of those with `EnsureCanAccessBeehiveAsync`
> ([DietService.cs:58](backend/Melarium.Application/Features/Diets/DietService.cs:58)), and the
> frontend explicitly grants it (`DietSection.tsx:83` — `canManageDiets || isAssignedToHive(id)`).
> Under an apiary-scoped model that no longer works: creating a programme means choosing hives across
> the apiary, which a Beekeeper cannot see. It now matches Tretmani, Vrcanja and Troškovi. Remove
> `|| isAssignedToHive(...)` from the frontend gate, and **put the loss in the release note** — this
> is a capability existing users have today.

> **② This widens Beekeeper scope on ticking rounds — DECIDED: allow.** Today a Beekeeper ticks a
> feeding on *their own hive's* programme. With apiary-level rounds, one tick marks the round done
> for every hive on it — including hives they are not assigned to. Treatments are a legal record and
> stay read-only for Beekeepers; feeding is field work the Beekeeper physically performs, so making
> them ask an admin to record it would be worse than what exists today.

## Data migration (`AddApiaryScopedDiets`) — production data, handle deliberately

Melarium is live; existing diets carry real feeding history. **EF Core will not generate a correct
migration for this** — scaffold it, then hand-edit to the order below. A generated
`ADD COLUMN "ApiaryId" integer NOT NULL DEFAULT 0` would either fail the FK or silently point every
diet at apiary 0.

> Raw SQL in a migration is the sanctioned exception to the "EF Core LINQ only" rule in `CLAUDE.md`
> — that rule governs repositories and queries. Data migrations have used `migrationBuilder.Sql(...)`
> before (the old-DB import).

```sql
-- 0. Abort loudly rather than corrupt: a diet whose hive is gone has no apiary to inherit.
--    The whole migration runs in one transaction, so RAISE EXCEPTION rolls everything back.
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM "Diets" d LEFT JOIN "Beehives" b ON b."Id" = d."BeehiveId"
             WHERE b."Id" IS NULL) THEN
    RAISE EXCEPTION 'SPEC-12: orphan diets found (Diets.BeehiveId with no Beehive) — aborting.';
  END IF;
END $$;

-- 1. New table (Id identity, CreatedAt NOT NULL, UpdatedAt NULL — BaseEntity shape)
CREATE TABLE "DietBeehives" (...);

-- 2. Nullable first
ALTER TABLE "Diets" ADD COLUMN "ApiaryId" integer NULL;
ALTER TABLE "Diets" ADD COLUMN "AmountPerHive" numeric(6,2) NULL;
ALTER TABLE "Diets" ADD COLUMN "AmountUnit" integer NULL;
ALTER TABLE "Diets" ADD COLUMN "AmountNote" character varying(100) NULL;
ALTER TABLE "FeedingEntries" ADD COLUMN "Note" character varying(300) NULL;

-- 3. Backfill: every diet inherits its hive's apiary
UPDATE "Diets" d SET "ApiaryId" = b."ApiaryId"
FROM "Beehives" b WHERE b."Id" = d."BeehiveId";

-- 4. Backfill the link table: each existing diet keeps exactly its one hive
INSERT INTO "DietBeehives" ("DietId", "BeehiveId", "RemovedOn", "CreatedAt")
SELECT d."Id", d."BeehiveId", NULL, d."CreatedAt" FROM "Diets" d;

-- 5. Only now enforce the constraint, then drop the old column and its dependents
ALTER TABLE "Diets" ALTER COLUMN "ApiaryId" SET NOT NULL;
-- + FK Diets.ApiaryId -> Apiaries.Id ON DELETE CASCADE, index on ApiaryId, index on StartDate
DROP INDEX IF EXISTS "IX_Diets_BeehiveId";
ALTER TABLE "Diets" DROP CONSTRAINT IF EXISTS "FK_Diets_Beehives_BeehiveId";
ALTER TABLE "Diets" DROP COLUMN "BeehiveId";
-- + partial unique index on DietBeehives (DietId, BeehiveId) WHERE "RemovedOn" IS NULL
```

Step 0 is safe to expect to pass: `Beehives → Diets` is already `OnDelete(Cascade)`, so no diet can
reference a missing hive. It is there because "should be impossible" and "is impossible" are
different things on live data, and the cost of being wrong is every diet pointing at apiary 0.

Verify the same thing by hand on a dump copy before deploying:
`SELECT COUNT(*) FROM "Diets" d LEFT JOIN "Beehives" b ON b."Id" = d."BeehiveId" WHERE b."Id" IS NULL;`
must return 0.

**Diets previously copied to N hives stay N separate programmes,** each with one hive. They are not
auto-merged — guessing which rows "were the same programme" from name + dates would be clever and
occasionally wrong, and it would rewrite completion history. Users consolidate manually if they want
to; new programmes get the new behaviour. State this in the release note.

`Down()` must be written and must not silently lose data: it can restore `BeehiveId` only for diets
with exactly one active hive. For multi-hive diets there is no correct answer — `Down()` should
therefore **throw**. Note this means `dotnet ef migrations script --idempotent` for a rollback will
fail by design; take a `pg_dump` before deploying, that is the real rollback.

## Impact on existing consumers

All but one of these compile against `Diet.BeehiveId` today and **will break at compile time** —
which is exactly what we want. The exception is `DailyAgendaService`, which touches diets only
through `CalendarObligation` and therefore changes behaviour **without** a compiler error; it is in
the table for that reason. This is the complete list; nothing else touches diets.

| File | Change |
|---|---|
| [CalendarObligationService.cs:45-70](backend/Melarium.Application/Features/Calendar/CalendarObligationService.cs:45) | See "Calendar scope" below — this is the one with a security edge. Keep the `feeding-{entryId}` stable key: the ICS UID is `{StableKey}@{host}`, so already-synced events **update in place** instead of duplicating. |
| [CalendarService.cs:60-104](backend/Melarium.Application/Features/Calendar/CalendarService.cs:60) | same scope fix; `CalendarFeedingEntryDto` gains `apiaryId` + `apiaryName` + `hiveCount`, loses its per-hive identity. `CalendarPage.tsx` renders the new fields. |
| [DailyAgendaService.cs:74-80](backend/Melarium.Application/Features/Reminders/DailyAgendaService.cs:74) | **No code change — listed because its output changes anyway.** It composes the 08:00 reminder from `CalendarObligation.Title` via `GatherAsync`, so the morning message becomes `🍯 Prehrana — {pčelinjak} (n košnica)` for every user the day the migration lands. It also inherits the Beekeeper scope filter for free. Verify the composed message once after Phase A rather than assuming; a silent wording change in a push notification is the kind of thing users report as a bug. |
| [StatsService.cs:48-49](backend/Melarium.Application/Features/Stats/StatsService.cs:48) | `Diets.FindAsync(d => beehiveIds.Contains(d.BeehiveId))` → `Diets.GetByApiaryIdsAsync(apiaryIds)`. **Filter by apiary, not by a join through `DietBeehives`** — otherwise a programme whose hives were all removed disappears from stats while still being listed on `/feedings`. `apiaryIds` is already computed two lines above. Fixes the inflated "Aktivne prehrane" count. |
| [AdvisorContextBuilder.cs:22,60](backend/Melarium.Application/Features/Advisor/AdvisorContextBuilder.cs:22) | Signature change: the three parameters `Diet? activeDiet, int dietCompleted, int dietTotal` collapse into `IReadOnlyList<DietActiveInfo> activeDiets` — `DietActiveInfo` already carries the counts. Emit one line per active programme (there are at most a handful), rendering the amount as `{amount} {unit}` with the note in parentheses when present — e.g. `1 L (1:1)`. |
| [AdvisorService.cs:237-249](backend/Melarium.Application/Features/Advisor/AdvisorService.cs:237) | replaces the two-step "load diets by hive, then reload with entries" with a single `GetActiveForBeehivesAsync([beehiveId])`, mirroring how `latestTreatment` is already fetched on line 259. |
| [WeeklySummaryService.cs:151-156](backend/Melarium.Application/Features/Alerts/WeeklySummaryService.cs:151) | `hiveIds.Contains(fe.Diet.BeehiveId)` → `apiaryIds.Contains(fe.Diet.ApiaryId)`. Counts **rounds**, not hive-rounds; wording in `WeeklyDigestBuilder` becomes "obavljenih hranjenja". |
| `Beehive.Diets` navigation | removed — check for stray usages (only the EF config references it today). |
| `DietServiceTests.cs`, `AdvisorContextBuilderTests.cs` | rewritten for the new shape. |

### Calendar scope — the part that must not be got wrong

The obvious swap `GetByBeehiveIdsAsync(scope.BeehiveIds)` → `GetByApiaryIdsAsync(scope.ApiaryIds)`
**leaks data for Beekeepers.** `CalendarAccessResolver` sets a Beekeeper's `ApiaryIds` to the apiaries
*containing* their assigned hives
([CalendarAccessResolver.cs:63-76](backend/Melarium.Application/Features/Calendar/CalendarAccessResolver.cs:63)).
A programme covering only hives they are **not** assigned to would then appear on their in-app
calendar and be pushed into their real Google/Apple calendar.

Required in both `CalendarService` and `CalendarObligationService`:

```csharp
var diets = (await _uow.Diets.GetByApiaryIdsAsync(scope.ApiaryIds))
    .Where(d => d.Status != DietStatus.StoppedEarly)
    .Where(d => d.Beehives.Any(db => db.RemovedOn == null && scope.BeehiveIds.Contains(db.BeehiveId)))
    .ToList();
```

For managers this is a no-op (their `BeehiveIds` covers the apiary). For a Beekeeper it enforces
exactly the read rule from the authorization table. `GetByApiaryIdsAsync` must therefore
`Include(d => d.Beehives)`.

A diet with **zero active hives** produces no calendar obligations (nothing to do in the field) but
stays listed in the app. The `.Any(...)` filter above gives that for free.

### The obligation shape changes too

`CalendarObligation` carries `BeehiveId` / `ApiaryId` and the description embeds a deep link built
from them ([CalendarObligationService.cs:35-41](backend/Melarium.Application/Features/Calendar/CalendarObligationService.cs:35)).
A feeding round is no longer about one hive:

- `BeehiveId` → `null`, `ApiaryId` → `diet.ApiaryId`.
- `Location` → apiary name (was hive name).
- Title → `🍯 Prehrana — {apiaryName} ({n} košnica)`.
- Description → `Program: {name}\nHrana: {food}\nKošnice: {n}` + `Otvori: {baseUrl}/feedings/{dietId}`
  (better than the apiary page — it lands on the checklist the user actually needs).
- `Link()` currently has no diet case; add one, or pass the URL in directly.

Because the UID is unchanged, existing synced events **update** to the new title rather than
duplicating — verify this on a real synced calendar, it is the whole point of keeping the key.

**Wording cleanup while we are here:** the app says "Prihrana" in the calendar and the advisor, and
"Prehrana" everywhere in the UI. Standardise on **"Prehrana"** in
`CalendarObligationService` and `AdvisorContextBuilder`. One-line change, and it stops the two words
looking like two different features.

## Frontend

### New: `/feedings` top-level page

Diets are currently reachable **only** from a hive detail page — there is no way to see all feeding
across the operation. Tretmani has had its own page since SPEC-08; this brings prehrana level with
it and is the concrete meaning of "prehrana na viši nivo".

- `FeedingsPage` (`/feedings`) — year selector (default current), grouped by apiary, newest first.
  Row: start date, name, food type, status badge, `n/m` rounds progress, hive count.
  Empty state: "Još nema programa prehrane."
- Sidebar item **"Prehrana"** (lucide `Leaf`, already used by `DietSection`), added to `getNavItems`
  in [Sidebar.tsx:29-49](frontend/src/shared/components/Sidebar.tsx:29) between Vrcanja and Tretmani.
  `visible: true` for all roles (Beekeepers see their own hives' programmes).
- **Routes in `App.tsx` — and a guard that must be added.** Today the three feeding routes sit in
  two ungated blocks: `feedings/:id` ([App.tsx:105](frontend/src/App.tsx:105)) and then
  `feedings/new` + `feedings/:id/edit` ([App.tsx:108-109](frontend/src/App.tsx:108)), commented
  *"all authenticated users (User allowed for assigned hives)"*. Two changes:
  - Add the bare `feedings` route **next to the detail route**, ungated — the list is readable by
    every role, exactly like `harvests` ([App.tsx:153](frontend/src/App.tsx:153)) and `treatments`
    ([App.tsx:161](frontend/src/App.tsx:161)).
  - **Wrap `feedings/new` and `feedings/:id/edit` in `<Route element={<RoleRoute
    allowedRoles={HIVE_MANAGERS} />}>`**, copying the harvests/treatments blocks verbatim. This is
    the route-level half of authorization ① and it is easy to miss: dropping
    `|| isAssignedToHive(...)` from `DietSection` only hides the *button*. Without the guard a
    Beekeeper can still reach `/feedings/new` by URL or from a bookmark, fill the whole form, and
    only discover the `403` on submit. React Router ranks by specificity rather than declaration
    order, so no reordering is needed beyond this.
- **In-app help (SPEC-14) — do not skip this.** `helpRoutes.ts` has `/feedings/new`,
  `/feedings/:id/edit`, `/feedings/:id` but no `/feedings`. Add it **after** the three specific
  patterns (first `matchPath` hit wins) and write the matching entry in `helpContent.ts`, the same as
  `/treatments` has. A new top-level page with no help button is an inconsistency users notice.

### Changed: `DietFormPage` (`/feedings/new`, `/feedings/:id/edit`)

- Step 1: pick apiary (pre-selected from `?apiaryId=`).
- Step 2: **checkbox table of that apiary's hives, all pre-checked** — identical component behaviour
  to `TreatmentFormPage`, so the two forms feel the same. This is the "tek onda odaberi košnice" step.
- Step 3: programme fields + the new amount row: **Količina po košnici** (decimal) + **jedinica**
  (select: L / ml / kg / g) + **Napomena** (`"npr. 1:1, pola pogače"`).
  The decimal field **must** use `decimalInputProps` + `sanitizeDecimal` / `parseDecimal` from
  [decimalInput.ts](frontend/src/shared/utils/decimalInput.ts) — a raw `type="number"` silently
  discards the keystroke on an iPhone with a Bosnian keyboard, which is exactly the bug that helper
  exists to prevent. The unit select is required as soon as a number is entered, and disabled (with
  a cleared value) when the number is empty.
- **Keep the existing `?beehiveId=` deep link working.** `DietSection` links to
  `/feedings/new?beehiveId={id}` today, and so do bookmarks. When `beehiveId` is present and
  `apiaryId` is not: resolve the hive's apiary, select it, and pre-check **only that hive**. This is
  the "one hive needs feeding" flow from the user stories — losing it would make the feature feel
  heavier than what it replaces.
- On **edit**, the hive checkboxes are not part of the form (hives are managed from the detail page
  via their own endpoints). Note this deliberately differs from `TreatmentFormPage`, where `PUT`
  *does* replace the entry set: treatment entries are disposable rows, `DietBeehive` rows carry
  removal history that a replace would silently destroy.

### Changed: `DietDetailPage`

- Header: apiary name, food type, amount per hive, status badge, progress.
- **"Košnice (n)"** section — chips with hive names, an **"Dodaj košnice"** button (`Modal` +
  checkbox list of hives of the apiary not currently active on the programme, including previously
  removed ones), and an × per chip that soft-removes with a `ConfirmDialog`. Removed hives render
  greyed with "uklonjena 12.09.".
- Rounds checklist unchanged, plus an optional note field on completion.
- "Dodaj košnice" is hidden when the diet is `Completed`/`StoppedEarly` (the backend returns 422).

### Changed: `DietSection` on `BeehiveDetailPage` → hive-oriented

Rename to `HiveFeedingCard` and align it with `HiveTreatmentCard` so the two cards on the same page
match. Shows the **active** programme(s) for this hive: name, food type + amount, `n/m` rounds, next
feeding date, and a "Historija (n)" link to `/feedings?beehiveId=`.

- **Data source: `GET /feedings?beehiveId=` (Phase A), not `/feedings/active` (Phase C).** The card
  ships in Phase B, one phase before `/feedings/active` exists, and `DietDto` already carries
  everything the card renders — `status`, `foodType`, the amount fields, `completedEntries` /
  `totalEntries` and `nextFeedingDate` — so the card filters client-side for
  `NotStarted`/`InProgress`. `/feedings/active` exists for the *apiary-wide* case (the badge and the
  hive-list chips in Phase C), where the alternative would be one request per hive. Do not pull that
  endpoint forward into B and do not issue N per-hive requests from a list.
- `isError` must render an error, never "nema prehrane" (same reasoning as `HiveTreatmentCard` — a
  failed request must not read as "this hive isn't being fed").
- **Keep an "Dodaj prehranu" action on the card** for users who can manage the apiary, pointing at
  `/feedings/new?beehiveId={id}`. Today's `DietSection` has it; dropping it would mean the only way
  to start feeding one hive is to navigate away to the apiary and untick nineteen others.
- The permission gate becomes plain `canManageDiets` — drop `|| isAssignedToHive(beehiveId)` (see
  authorization ①).

### New: the active-feeding indicator (the explicit ask)

- **BeehiveDetailPage** — a badge next to the hive name: `🌿 Aktivna prehrana` (honey tint), with
  "sljedeće hranjenje {date}" underneath. Uses `GET /feedings/active?apiaryId=`. If a hive has more
  than one active programme, show the badge once with the **earliest** next feeding date and a `×2`
  suffix; the card below lists them all.
- **ApiaryDetailPage hive list** — a small leaf chip on each hive row that has an active programme,
  from the same single request (not one per hive).
- **`ApiaryFeedingsSection` on ApiaryDetailPage** — mirrors the existing `ApiaryTreatmentsSection` /
  `ApiaryHarvestsSection`: this apiary's programmes, newest first, with a link to `/feedings`. This is
  the natural "create a programme for this apiary" entry point and the pattern is already established
  twice; leaving it out is the odd one out.
- Styling: reuse the `badge` class and the honey/amber palette already in `DietSection`'s
  `STATUS_STYLES`. (Note: there is no existing per-hive chip on the apiary hive list to copy from —
  treatments only have a card on the hive page — so this is new ground, not a mirror.)

### Deleted

`CopyDietDialog.tsx`, its query hook, `copyDiet` in `dietService.ts`, `CopyDietPayload` in
`models/index.ts`, and the `/copy` route usage.

### Query layer

Diet hooks live in `queries.ts` today while treatments have their own `treatmentQueries.ts`. Move
diet hooks to a new **`dietQueries.ts`** to match: `useDiets(filters)`, `useDiet(id)`,
`useActiveDiets(apiaryId)`, plus create/update/delete/add-hives/remove-hive/complete-round/
complete-early mutations. Key structure: `['diets']`, `['diets', id]`, `['diets','active',apiaryId]`.

Every mutation invalidates `['diets']` **and** `['calendar']` (the calendar shows the same
obligations). Round completion and hive add/remove additionally invalidate `['diets','active']` so
the hive badge updates, and `['stats']`. Remove `queryKeys.dietsByBeehive` from `queries.ts`.

## Feeding cost (Phase E)

Feeding sugar is one of the real recurring costs of a beekeeping operation, and this is the first
point in the app where the data to quantify it exists. Phase E connects the programme to the existing
Expenses module (SPEC-09 era) without inventing any numbers.

### Two figures, deliberately kept apart

| | **Potrošnja** (planning) | **Trošak** (accounting) |
|---|---|---|
| Source | computed from the programme itself | sum of attributed `ExpenseItem` rows |
| Unit | the programme's own unit (L / kg / …) | money (`Expense.Currency`) |
| Answers | "koliko šećera da kupim za zimsku prehranu" | "koliko me je ova prehrana koštala" |
| Missing when | `AmountPerHive` is null | nothing has been attributed yet |

They are shown side by side and **never subtracted from or converted into each other**. A litre of
1:1 syrup is not a kilogram of sugar, and any automatic conversion would be a guess dressed as a
fact. Same reason there is **no estimated price**: deriving a unit price from "the last similar
purchase" produces an authoritative-looking money figure that nobody entered. Consumption is exact;
cost is exact or absent.

### Potrošnja — exact, not approximated

The naive formula `amount × hive count × rounds` is wrong the moment a hive joins or leaves
mid-programme. The soft-remove model already carries what is needed to be exact, so use it: for each
round, count the hives that were on the programme **on that round's date** —

```
hives_on(date) = DietBeehives where CreatedAt.Date <= date.Date
                 and (RemovedOn is null or RemovedOn.Value.Date >= date.Date)

planirano = Σ over all rounds     ( AmountPerHive × hives_on(round.ScheduledDate) )
do sada   = Σ over completed rounds ( AmountPerHive × hives_on(round.ScheduledDate) )
```

**Compare dates, never raw timestamps — this is the one place the formula is easy to get wrong.**
`BaseEntity.CreatedAt` is `DateTime.UtcNow` and carries a time of day
([BaseEntity.cs:9](backend/Melarium.Domain/Common/BaseEntity.cs:9)), while `ScheduledDate` is a
midnight date. A programme created today at 14:30 whose first round is scheduled for today would
evaluate `14:30 <= 00:00` as false and count **zero** hives for that round — a 10-hive × 12-round
programme would report 110 L instead of 120 L. `.Date` on both sides removes the whole class of bug,
and the migration's backfilled rows (`CreatedAt` copied from the diet) are subject to exactly the
same trap.

The removal day **counts as fed** (`RemovedOn.Value.Date >= date.Date`): `RemovedOn` is clamped to
today server-side, and a hive removed on the morning of a round it was on until that moment did
receive that round's feed. Removing a hive therefore never retroactively lowers a *completed*
round's figure — only future rounds shrink.

Both collections are already loaded by `GetWithEntriesAsync` and are small (tens of rows), so this is
an in-memory fold in the service, not a query. This is the second time the soft-remove pays for
itself — worth noting in the ADR.

### Trošak — attribution, not a new ledger

```
ExpenseItem : BaseEntity                   // CHANGED — one nullable FK
  DietId   int?   (FK Diet, ON DELETE SET NULL)   // NEW
  Diet     Diet?                                   // NEW navigation
```

**`SET NULL`, never cascade.** An expense is an accounting record of money that actually left the
account; deleting a feeding programme must never delete it. Index on `DietId`.

- New repository method `IExpenseRepository.GetTotalsByDietsAsync(IEnumerable<int> dietIds)` →
  `Dictionary<int, List<(string Currency, decimal Total)>>`, grouped **by currency**. Summing across
  currencies would be a silent lie; in practice everything is BAM and the list has one entry.
- Derived on the detail DTO: **cijena po košnici** = total ÷ active hive count, only when the count
  is > 0 and there is exactly one currency.
- No new entity, no new table, no change to how expenses are created or totalled.

### The trap in `ExpenseService.UpdateAsync`

`UpdateAsync` does `expense.Items.Clear()` and then re-maps the whole collection from the DTO
([ExpenseService.cs:75-76](backend/Melarium.Application/Features/Expenses/ExpenseService.cs:75)) —
line items are destroyed and recreated on **every** edit. If `dietId` is not carried through
`CreateExpenseItemDto` *and* re-sent by the expense form, editing an unrelated field on the receipt
silently drops every attribution on it, with no error. This is the single most likely bug in Phase E;
there is an acceptance criterion for exactly this.

AutoMapper needs no change: `CreateMap<CreateExpenseItemDto, ExpenseItem>()` and
`CreateMap<ExpenseItem, ExpenseItemDto>()` are convention-based
([ExpenseMappingProfile.cs:23,34](backend/Melarium.Application/Features/Expenses/ExpenseMappingProfile.cs:23)),
so adding the property to both sides is picked up automatically — no existing `CreateMap` is edited,
so `ignore.md`'s mapping rule is not crossed.

### Authorization — money is not visible to everyone

`canSeeExpenses` is `SystemAdmin | OrgAdmin | ApiaryAdmin`
([usePermissions.ts:36](frontend/src/core/hooks/usePermissions.ts:36)) — a Beekeeper has no access to
the Expenses module at all. Therefore:

- **Potrošnja** is visible to everyone who can read the diet (it is a quantity, not money).
- **Trošak** is **omitted from the DTO** for Beekeepers — null on the server, not merely hidden with
  CSS. A hidden-but-transmitted number is still a leak.
- Attributing an item requires org membership **and** `EnsureCanManageApiaryAsync(diet.ApiaryId)`.
  `Expense` is organization-scoped while `Diet` is apiary-scoped, so the service must verify the
  diet's apiary belongs to the caller's organization → otherwise `400`, not `404` (the id is well
  formed, it is just not attributable).

### Endpoints & UI

| Where | Change |
|---|---|
| `GET /feedings/{id}` | `DietDetailDto` gains `plannedAmount`, `consumedAmount`, `amountUnit`, and (managers only) `costTotals: [{ currency, total }]` + `costPerHive` |
| `GET /feedings?year=` | `DietDto` gains the same cost fields so the list can total them |
| Expense form | optional **"Program prehrane"** select per line item — options are the org's programmes from the current and previous year, newest first, plus an empty default |
| `DietDetailPage` | "Potrošnja i trošak" block; when nothing is attributed, a "Poveži trošak" link to `/expenses/new?dietId={id}` |
| `FeedingsPage` | year footer: "Trošak prehrane {year}: X BAM" |
| `StatsService` | `FeedingCost` on `StatsDto`, next to the existing `SeasonTotalKg` / `EstimatedRevenue` — one query, symmetric with what is already there |

### Migration (`AddFeedingCostAttribution`)

A **second, separate** migration — additive, no data movement, trivially reversible. Do not fold it
into `AddApiaryScopedDiets`; that one is dangerous and this one is not, and mixing them means a
rollback of the cheap change drags the expensive one with it.

```sql
ALTER TABLE "ExpenseItems" ADD COLUMN "DietId" integer NULL;
-- + FK ExpenseItems.DietId -> Diets.Id ON DELETE SET NULL, index on DietId
```

`Down()` drops the column — no data loss beyond the attributions themselves, which are recoverable
from the programmes by hand.

### Out of scope for Phase E

Automatic price estimation from past purchases; unit conversion between the programme's unit and a
purchase's unit; splitting one expense line across several programmes (buy for two programmes → enter
two line items, which the form already supports); per-hive cost beyond the single
`trošak ÷ košnice` figure; and any cost figure on the treatment side.

## Frozen-area exceptions (`ignore.md`)

`CLAUDE.md` requires checking `ignore.md` before every task, and this spec deliberately crosses three
of its lines. Listing them here so the implementer does not stop at the checklist:

| Frozen rule | What this spec changes | Why it is allowed |
|---|---|---|
| "Do not change existing entity configurations" | `DietConfiguration`, `BeehiveConfiguration`, `FeedingEntryConfiguration` | Explicitly directed by this spec, and backed by the hand-written migration above. |
| "Never edit existing migration files" | not violated | A **new** migration is added; nothing existing is edited. |
| "Never rename/remove existing interface properties" (`core/models/index.ts`) | `Diet.beehiveId` removed, `CopyDietPayload` deleted | Backend + frontend deploy together; TypeScript catches the removals because the whole diet slice is being rewritten in the same PR. |

## Decisions taken (2026-07-30)

1. **Beekeeper loses create / edit / delete / stop.** Accepted as a deliberate narrowing — see
   authorization ①. Goes in the release note.
2. **Beekeeper may tick feeding rounds**, including for hives they are not assigned to — see
   authorization ②.
3. **Re-adding a removed hive creates a new `DietBeehive` row**; the old one stays as history with
   its `RemovedOn` date. This is why the unique index is partial (`WHERE "RemovedOn" IS NULL`) — a
   plain unique index would make re-adding impossible.
4. **Amount per hive is number + unit + note**, all optional, note valid on its own — see "Amount per
   hive" under Entities. This deliberately does not copy the free-text `DosePerHive` shape, and it is
   what keeps a future cost figure reachable.
5. **The "hranjenje kasni" alert ships in this spec as Phase D**, after the migration is live.
6. **Feeding cost ships in this spec as Phase E** — consumption computed from the programme, money
   attributed from existing `ExpenseItem` rows. No invented price estimates.

## Open questions

1. **Existing copied programmes.** **Run this on production first** — the answer may make the
   question moot:

   ```sql
   SELECT d."Name", d."StartDate", b."ApiaryId", COUNT(*) AS kopija
   FROM "Diets" d JOIN "Beehives" b ON b."Id" = d."BeehiveId"
   GROUP BY 1, 2, 3 HAVING COUNT(*) > 1
   ORDER BY kopija DESC;
   ```

   - **0 rows** → nobody ever used copy in anger; delete this question and ship.
   - **Some rows** → decide with the real number in hand. Default stays **leave them as N
     single-hive programmes** (an automatic merge cannot answer "which completion count is the true
     one" when one copy is 5/10 and another 3/10 — any choice rewrites someone's history). If the
     mess is large enough to matter, the fix is a **manual** "Spoji programe" action in a separate
     spec, where the user sees the result before confirming — not something the migration does behind
     their back.

*(Cost tracking was the second open question; it is now specified in "Feeding cost (Phase E)" above.)*

## Edge cases

- Diet whose hives were all removed or deleted → still listed with hive count 0, rounds intact. Not
  an error. Produces **no** calendar obligations, but still counts in stats (it is a live programme).
- Hive deleted mid-programme → `DietBeehive` cascade-deletes with it (same documented v1 trade-off as
  `TreatmentEntry`). The rounds and the programme survive.
- Hive **moved to another apiary** (SPEC-10 selidba moves the whole apiary, so this is only hive
  reassignment): the `DietBeehive` link is left as-is and the diet keeps its original `ApiaryId`.
  Validation only checks apiary membership **at the time hives are added**, not retroactively.
- Hive removed and later re-added → two `DietBeehive` rows; only the second is active. The detail
  page shows both, the count shows 1.
- Adding a hive to a programme that is already `Completed` or `StoppedEarly` → `422`. Only
  `NotStarted`/`InProgress` accept new hives.
- Removing the **last** hive from a programme → allowed, and the programme is not auto-stopped.
  The beekeeper stops it explicitly via "Zaustavi ranije" so the reason is recorded.
- `RemovedOn` in the future, or before the diet's `StartDate` → clamp to today; it is set server-side,
  never accepted from the client.
- Hive on **two** active programmes → the badge shows once (earliest next feeding, `×2` suffix); the
  advisor context lists both lines; `/feedings/active` returns two rows for that hive.
- Winter programme spanning New Year → belongs to the **start year** (year filter on `StartDate`),
  same convention as treatments.
- Beekeeper with no assigned hives → empty list, **not** 403.
- Beekeeper opening `/feedings?apiaryId=X` for an apiary where none of their hives are on any
  programme → empty list, not 403 (they can access the apiary; there is just nothing to show).

## Out of scope (v1)

Per-hive round tracking (see the model decision), templates/presets for common programmes,
automatic weather-based feeding suggestions,
per-round actual amounts, PDF export of a feeding register (no legal requirement, unlike treatments),
offline queueing of round completion (the outbox covers inspections only today), and any change to
`DietReason`/`FoodType` enums.

## Phases

**A and B must ship together** — the migration breaks the current frontend. C, D and E are each
independently shippable afterwards, in any order.

- **Phase A — backend.** Entities, EF config, hand-written migration, repository, service,
  validators, controller, the six consumer fixes (incl. the calendar scope filter), tests.
  Nothing user-visible.
- **Phase B — frontend.** `dietQueries.ts`, `FeedingsPage` + sidebar + help entry, reworked
  `DietFormPage` (incl. `?beehiveId=` deep link) and `DietDetailPage`, `HiveFeedingCard`,
  delete `CopyDietDialog`.
- **Phase C — indicator + polish.** `GET /feedings/active`, hive badge on BeehiveDetailPage, chips on
  the ApiaryDetailPage hive list, `ApiaryFeedingsSection`, advisor context line, calendar wording.
- **Phase D — "hranjenje kasni" alert (SPEC-04).** Independently shippable; do it after A is live so
  a rule never fires against a half-migrated schedule.
- **Phase E — trošak prehrane.** `ExpenseItem.DietId` + its own small migration, the consumption fold,
  the cost totals query, the expense-form select, the diet detail block, `StatsDto.FeedingCost`.
  Depends on A + B (needs the amount fields and the hive-link history); independent of C and D.
  Full detail in "Feeding cost (Phase E)" above.

### Phase D in detail

`AlertRuleService` already has the shape to copy: `ApplyTreatmentRulesAsync`
([AlertRuleService.cs:144-174](backend/Melarium.Application/Features/Alerts/AlertRuleService.cs:144))
is ~30 lines — load per apiary, loop, `DispatchAsync` with a dedup cooldown, gated by a config flag.

- New `ApplyFeedingRulesAsync(apiary, now, overdueDays, enabled)`: for each diet of the apiary whose
  status is `InProgress`, fire when a `Pending` round has `ScheduledDate <= now - overdueDays` **and**
  the diet still has at least one active hive (no hives → nothing to do in the field, same rule the
  calendar uses). Fire once per **diet**, not per round — a programme two weeks behind should produce
  one nudge, not seven.
- `NotificationType.FeedingOverdue = 23` (next free value — 22 is `FeedbackStatusUpdated`).
- Config: `Alerts:FeedingOverdue:Enabled` (default true) + `Alerts:FeedingOverdueDays` (default 2),
  read via the existing `GetBool` / `GetInt` helpers.
- Message, Bosnian: *"Hranjenje kasni — pčelinjak '{apiary}': runda zakazana za {date} još nije
  označena."* Dedup cooldown `TimeSpan.FromDays(3)`.
- `BsLabels` label for the new notification type + the frontend label map, plus one test in the
  alerts test file.

## Acceptance criteria

- [ ] Creating one programme for an apiary with 20 hives produces **1** `Diet`, **20** `DietBeehive`
      rows and **`duration/frequency`** `FeedingEntry` rows — not 20 diets.
- [ ] The calendar (in-app **and** the ICS feed) shows **one** obligation per feeding date for that
      programme, not 20. Verified against a real 20-hive apiary.
- [ ] A previously synced feeding event **updates in place** in a real Google/Apple calendar after
      the migration (the `feeding-{entryId}` UID is unchanged) — no duplicate event appears.
- [ ] A Beekeeper assigned to hive A does **not** see, on their calendar or ICS feed, a programme of
      the same apiary that covers only hives B and C.
- [ ] Migration on a copy of the production dump: every existing diet keeps its rounds, its
      completion history and its hive, and lands on the correct apiary. Row counts before/after match.
- [ ] Migration aborts cleanly (transaction rolled back, nothing changed) when an orphan diet exists.
- [ ] Hives from another apiary in `beehiveIds` → `400`. Duplicate ids → `400`.
- [ ] A hive removed from a programme can be **re-added**; the detail page then shows the old link as
      history and the new one as active, and the hive count is 1.
- [ ] Opening a hive with a running programme shows the **"Aktivna prehrana"** badge with the next
      feeding date; a hive without one shows nothing (and a failed request shows an error, not "nema").
- [ ] A hive on two overlapping programmes shows one badge and both programmes on the card.
- [ ] Removing a hive mid-programme keeps it visible in the programme's history with its removal
      date, and clears its badge.
- [ ] `/feedings/new?beehiveId={id}` still works: apiary resolved automatically, only that hive
      pre-checked.
- [ ] Role matrix enforced exactly as tabled, including a Beekeeper ticking a round for hives they
      are not assigned to, and a Beekeeper getting `403` on create.
- [ ] A Beekeeper who types `/feedings/new` directly into the address bar is bounced by `RoleRoute` —
      they never reach the form and then a `403` on submit.
- [ ] `PUT /feedings/{id}` does not change the hive set; only the two `/beehives` endpoints do.
- [ ] `POST /feedings/{id}/copy` is gone (`404`) and `CopyDietDialog` is deleted with no dead imports.
- [ ] Stats "Aktivne prehrane" counts programmes, not hive-programmes — including a programme whose
      hives were all removed.
- [ ] `/feedings` has a sidebar entry **and** a help entry; `resolveHelpKey('/feedings')` returns
      `/feedings` and not `/feedings/:id`.
- [ ] Amount: `1` + `L` + `1:1` round-trips through create → detail → edit; a note alone with no
      number saves fine; a number with no unit is rejected `400`.
- [ ] The decimal amount field accepts `1,5` **on an iPhone with a Bosnian keyboard** and stores
      `1.50` (this is the `decimalInput.ts` bug class — verify on a device, not just desktop).
- [ ] Phase D: a programme with a round pending 3+ days produces **one** "Hranjenje kasni"
      notification, not one per overdue round; a programme with zero active hives produces none;
      `Alerts:FeedingOverdue:Enabled=false` suppresses it.
- [ ] Phase E: a programme of 10 hives × 1 L × 12 rounds reports `planirano 120 L`; after a hive is
      removed following round 6, the figure drops to `114 L` — the count is taken **per round date**,
      not from today's hive count.
- [ ] Phase E: **editing an unrelated field on a receipt does not clear its programme attribution**
      (this is the `Items.Clear()` trap — the regression test belongs in the expense tests, not the
      diet tests).
- [ ] Phase E: deleting a feeding programme leaves the attributed expense intact with `DietId` null —
      the expense total for the organization is unchanged.
- [ ] Phase E: a Beekeeper reading a diet gets `costTotals`/`costPerHive` as **null from the server**,
      while `plannedAmount`/`consumedAmount` are present.
- [ ] Phase E: attributing an item to a programme of another organization → `400`.
- [ ] All new strings Bosnian; amount labels "Količina po košnici" / "Napomena"; "Prihrana" no longer
      appears anywhere (standardised to "Prehrana").
- [ ] Docs updated: `features/diets.md` (**its enum list and API table are both stale — `DietReason`
      there does not match `Melarium.Domain/Enums/DietReason.cs`, and the paths say `/diets`**),
      `api-contracts.md` (same `/diets` → `/feedings` correction, plus `dietId` on the expense item
      contract), `context.md` (same), `smart-alerts.md` (the Phase D rule),
      **[queens.md:43](docs/features/queens.md:43)** (it cites diets as an example of the
      "Beekeeper assigned to the hive may manage it" rule — that stops being true here, and the
      same sentence sits in the `DietsController` XML doc comment
      [DietsController.cs:10-11](backend/Melarium.API/Controllers/DietsController.cs:10)),
      `decisions.md` (an ADR for the diet-level-rounds decision, noting that the soft-remove history
      is what makes exact consumption possible), this spec → ✅.

## Revision log

- **v2 (review pass, 2026-07-30)** — corrected the route prefix from `/api/diets` to the actual
  `/api/feedings`; added the Beekeeper create/edit narrowing that v1 missed; added the calendar
  scope filter that prevented a cross-hive data leak for Beekeepers; made the `(DietId, BeehiveId)`
  unique index partial so a removed hive can be re-added; changed `GetActiveForBeehivesAsync` to
  return a list per hive because overlapping programmes are allowed; switched the stats fix from a
  hive join to an apiary filter; kept the round-completion endpoint returning `DietDetailDto`;
  added the migration abort mechanism, the `?beehiveId=` deep-link requirement, the help-registry
  entry, `ApiaryFeedingsSection`, and the frozen-area exception table.
- **v2.1 (2026-07-30)** — the three questions raised by the review are answered and moved into
  "Decisions taken": Beekeeper write access narrowed, Beekeeper round-ticking allowed, re-add creates
  a new link row.
- **v2.2 (2026-07-30)** — remaining open questions answered: `AmountPerHive` becomes
  **decimal + `FeedingAmountUnit` + free-text note** (replacing the single free-text field, and
  keeping a future cost figure reachable); the "hranjenje kasni" alert becomes **Phase D** of this
  spec rather than a separate follow-up; the copied-programmes question now starts with a production
  count query instead of a guess. Also noted that `features/diets.md` has a stale `DietReason` list.
- **v2.4 (2026-08-05, pre-implementation pass)** — five gaps closed before starting, all found by
  checking the spec's own file references against the code: the Phase E consumption formula now
  compares **dates, not timestamps** (`CreatedAt` carries a time of day, so the first round of a
  same-day programme counted zero hives) and states that the removal day counts as fed;
  `HiveFeedingCard` gets an explicit data source (`GET /feedings?beehiveId=` in Phase B —
  `/feedings/active` does not exist until Phase C); `DailyAgendaService` is added to the consumer
  table as the one consumer whose output changes without a compile error; the **`RoleRoute` guard on
  `/feedings/new` and `/feedings/:id/edit`** is now spelled out (authorization ① is not enforced by
  hiding a button, and the old claim about `App.tsx` route ordering was inverted); and the
  round-completion endpoint is flagged as **gaining a request body it does not have today**, so it
  needs its own DTO and validator.
- **v2.3 (2026-07-30)** — feeding cost promoted from an open question to **Phase E**: `ExpenseItem.DietId`
  with `ON DELETE SET NULL`, exact per-round-date consumption, cost totals grouped by currency, and no
  estimated prices or unit conversion. Flagged the `ExpenseService.UpdateAsync` `Items.Clear()` trap
  that would otherwise wipe attributions on every receipt edit, and made the cost figures
  server-side-null for Beekeepers.
