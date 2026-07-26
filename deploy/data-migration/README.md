# One-off data migration — old production → current production

> **Status: done.** Rehearsed on a restored dump and then run against production on
> **2026-07-26**. All of it landed: 2 organizations, 3 users, 2 apiaries, 13 beehives,
> 9 inspections, 6 queens, 6 diets, 30 feeding entries, 2 todos, 10 learning topics,
> 0 treatments. QR codes were regenerated afterwards and the staging schema dropped.
>
> These scripts are kept for the audit trail, not for re-use. `02_import.sql` aborts on
> its e-mail pre-flight check if run again, so a second run cannot duplicate the data —
> but do not reach for it as a template without re-reading the whole file first. The id
> mapping was exported to `~/melarium-id_map.csv` on the VPS before cleanup; the
> pre-import dump is `~/melarium-before-import.dump`.

Imports a **subset** of the old Melarium database (delivered as CSV exports of its
tables) into the live database. Only two organizations and their data are carried
over; everything else in the export is test/demo data and is deliberately left
behind.

The old export matches the current schema column-for-column, with one exception
(`Users.EmailVerifiedAt`, added after the export — see [Decisions](#decisions)).
This is a data subset copy, not a schema conversion.

## What gets imported

| Table | Rows | Notes |
|---|---:|---|
| `Organizations` | 2 | `AS Honey House` (old id 6), `Ćatić-Mekić family` (old id 7) — both on the `Partner` plan |
| `Users` | 3 | Asim Haskić, Amel Ćatić, Semin Alihodžić — all `OrganizationAdmin` |
| `Apiaries` | 2 | `Dolac, Vakuf` (org 6), `Crniče` (org 7) |
| `Beehives` | 13 | 6 under Dolac Vakuf, 7 under Crniče |
| `Inspections` | 9 | |
| `Queens` | 6 | |
| `Diets` | 6 | |
| `FeedingEntries` | 30 | |
| `Todos` | 2 | |
| `Treatments` | 0 | every treatment in the export belongs to a test organization |
| `TreatmentEntries` | 0 | |
| `LearningTopics` | 10 | **optional**, separate script — platform-wide content, not org data |

Everything is derived from a single list — `staging.included_org` in
`01_load_csv.sql`, holding old organization ids `6` and `7`. Users, apiaries,
hives, inspections, queens, diets, feeding entries, treatments and todos are all
selected by following foreign keys from there, so there is no second filter to
keep in sync. Adding a third organization means editing that one list and the
`expected_counts` table in `02_import.sql`.

### Not imported

- **Users outside those two organizations** — the demo/test accounts
  (`sysadmin@beehive.com`, `orgadmin@beehive.com`, `test@beehive.com`,
  `testadmin@beehive.com`, `testadmin2@beehive.com`) and organizations
  `Test Org` (old ids 8 and 9), with all their apiaries, hives and records.
- **Tables not present in the export**: `Harvests`, `HarvestEntries`, `Expenses`,
  `ExpenseItems`, `InspectionPhotos`, `QueenEditLogs`, `Pastures`, `ApiaryMoves`,
  `CalendarSettings`, `Notifications`, `AdvisorConversations`/`AdvisorMessages`,
  `LearningTopicReads`, `RefreshTokens`, `UserTokens`. If the old database holds
  harvests or expenses for these two organizations, export those tables too —
  they are not recoverable later without the old database.
- `UserBeehives` — not needed: hive-level assignments only apply to the
  `Beekeeper` role and all three imported users are `OrganizationAdmin`.
- `RefreshTokens` / `UserTokens` — session and one-time-token state; imported
  users simply log in again.

## How ids are handled

**No old id is reused.** Every row receives a fresh id drawn from its table's own
identity sequence via `nextval()`, recorded in `staging.id_map(entity, old_id,
new_id)`; every foreign key is translated through that map. Consequences:

- The import cannot collide with rows that already exist in production.
- The sequences are correct afterwards by construction — no `setval()` fix-up at
  the end that could be forgotten.
- `staging.id_map` is the audit trail. **Export it before running
  `99_cleanup.sql`** (the command is in that file).

Hive QR codes survive the remap: the QR image encodes
`<frontend>/scan/<UniqueId>`, and `UniqueId` (a GUID) is carried over verbatim.

`sync_seq()` runs before each allocation and lifts every sequence past
`MAX("Id")`. This is not cosmetic. The seeded rows in `Organizations`,
`Apiaries`, `Beehives` and `Inspections` (ids 1–4, written by `InitialCreate`
through `InsertData`) never advanced their identity sequences, so on a database
where nothing has yet been created through the app, the *application's* next
insert would try id 1 and hit a seeded row. Section 6 of `04_verify.sql` reports
this for every table; the import repairs it as a side effect.

## Prerequisites

- The 12 CSV files, named exactly as exported: `Organizations_rows.csv`,
  `Users_rows.csv`, `Apiaries_rows.csv`, `Beehives_rows.csv`,
  `Inspections_rows.csv`, `Queens_rows.csv`, `Todos_rows.csv`, `Diets_rows.csv`,
  `FeedingEntries_rows.csv`, `Treatments_rows.csv`,
  `TreatmentEntries_rows.csv`, `LearningTopics_rows.csv`.
- **`Diets` must still be hive-scoped.** These scripts write `Diets.BeehiveId`.
  If [SPEC-12](../../docs/specs/SPEC-12-apiary-feeding.md) has already been
  deployed, that column is gone and `02_import.sql` will fail on it — run this
  migration *before* SPEC-12, or rework the `Diets` insert to populate
  `ApiaryId` + `DietBeehives`.
- A verified backup (below).

## Running it

> **Never commit the CSV exports.** They hold real e-mail addresses and BCrypt
> password hashes, and this repository is public. `.gitignore` in this directory
> blocks `*.csv` and `*.dump`; move them to the server with `scp`.

Every command below wraps the psql/pg_dump call in `sh -c '…'` with **single
quotes** so `$POSTGRES_USER` and `$POSTGRES_DB` are expanded inside the
container, where they exist. Double quotes would let the host shell expand them
first — and the host has no such variables, so the command would silently run as
the wrong user.

### 1. Get the files onto the server

The `.sql` files arrive with `git pull`. Only the CSVs need copying:

```bash
ssh root@melarium.app 'mkdir -p /tmp/melarium-migration'
```

```bash
scp "$USERPROFILE/Downloads"/*_rows.csv root@melarium.app:/tmp/melarium-migration/
```

Then on the server, from the repo root:

```bash
git pull && cp deploy/data-migration/*.sql /tmp/melarium-migration/
```

```bash
docker compose cp /tmp/melarium-migration postgres:/tmp/melarium-migration
```

The `\copy` paths inside `01_load_csv.sql` are absolute container paths
(`/tmp/melarium-migration/...`), so the working directory does not matter.

### 2. Back up

```bash
docker compose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > ~/melarium-before-import.dump
```

Confirm it is not empty and that pg_restore can read it before going further:

```bash
ls -lh ~/melarium-before-import.dump && pg_restore -l ~/melarium-before-import.dump | head
```

### 3. Rehearse on a copy — not on production

```bash
docker run -d --name melarium-rehearsal -e POSTGRES_USER=melarium -e POSTGRES_DB=melarium -e POSTGRES_PASSWORD=rehearsal postgres:16-alpine
```

```bash
docker cp ~/melarium-before-import.dump melarium-rehearsal:/tmp/ && docker cp /tmp/melarium-migration melarium-rehearsal:/tmp/melarium-migration
```

```bash
docker exec melarium-rehearsal sh -c 'pg_restore -U melarium -d melarium --no-owner --no-privileges /tmp/melarium-before-import.dump'
```

Now run steps 4–6 against the rehearsal container, substituting:

```bash
docker exec melarium-rehearsal sh -c 'psql -U melarium -d melarium -v ON_ERROR_STOP=1 -f /tmp/melarium-migration/01_load_csv.sql'
```

When `04_verify.sql` looks right there, tear it down and do it for real:

```bash
docker rm -f melarium-rehearsal
```

### 4. Load into staging

Nothing here touches a live table.

```bash
docker compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -f /tmp/melarium-migration/01_load_csv.sql'
```

Check the printed `in_csv` / `selected` table against [What gets
imported](#what-gets-imported) before continuing.

### 5. Import

One transaction. Every check runs before the first write; any failure rolls the
whole thing back.

```bash
docker compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -f /tmp/melarium-migration/02_import.sql'
```

Optionally, the learning topics (skips any article whose title already exists):

```bash
docker compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -f /tmp/melarium-migration/03_learning_topics.sql'
```

### 6. Verify

```bash
docker compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /tmp/melarium-migration/04_verify.sql'
```

Section 5 must be all zeros and section 6 must be `ok = true` on every row.

### 7. After the import

1. Log in as each of the three users and confirm their apiaries and hives are
   there. Passwords are unchanged — the hashes are BCrypt in the same format.
2. As SystemAdmin, call `POST /api/beehives/regenerate-qr-codes`. The imported
   `QrCodeBase64` images were generated against the *old* frontend URL; this
   re-renders them for the current domain. Already-printed labels are unaffected
   by the import itself (they resolve by `UniqueId`), but any label printed from
   now on should carry the new domain.
3. Export `staging.id_map` (command is in `99_cleanup.sql`), then run
   `99_cleanup.sql`.
4. Delete the CSVs from the server and from the container — they contain
   password hashes:

```bash
rm -rf /tmp/melarium-migration && docker compose exec -T postgres rm -rf /tmp/melarium-migration
```

## Rollback

The import is one transaction, so a failure needs no rollback — nothing was
written. Reverting a *successful* import means restoring the dump from step 1:

```bash
docker compose stop api
```

```bash
docker compose exec -T postgres sh -c 'pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists' < ~/melarium-before-import.dump
```

```bash
docker compose start api
```

Deleting the imported rows by hand instead is possible (`staging.id_map` has
every new id) but cascade behaviour makes ordering fiddly — prefer the restore.

Re-running after a failed attempt is safe. The only trace a rolled-back run
leaves is advanced identity sequences: sequence values are not transactional, so
ids may have gaps. Harmless.

Re-running after a *successful* import aborts on the e-mail pre-flight check —
the three addresses now exist — so a double-run cannot duplicate the data.

## Decisions

Things the scripts decide rather than ask, each with the reasoning:

- **`EmailVerifiedAt` is set to the account's `CreatedAt`.** The old database
  predates e-mail verification so the export has no such column. Leaving it NULL
  would show three live users a permanent "confirm your address" prompt. This is
  the same grandfathering that migration
  `20260725105931_AddUserTokensAndEmailVerification` applied to every account
  that existed when the feature shipped.
- **E-mails are lower-cased and trimmed** on insert; login looks the address up
  lower-cased.
- **`Organizations.CreatedById` and `Users.ApiaryId` are inserted as NULL and
  filled in by a follow-up `UPDATE`.** Both are halves of a circular foreign key
  (org → user → org, user → apiary → user). In this data both are already NULL,
  but the two-step keeps the script correct for any future export.
- **A pre-existing e-mail address aborts the run** rather than merging or
  skipping. If one of the three accounts was already re-created on the new
  production, merging is a judgement call, not something to guess at.
- **Row counts are asserted, not discovered.** `expected_counts` in
  `02_import.sql` pins the reviewed numbers; a re-export with different content
  stops the script instead of importing something nobody looked at.
