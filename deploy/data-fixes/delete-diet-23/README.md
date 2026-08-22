# One-off data fix — delete feeding programme (Diet) 23

Hard-deletes the feeding programme with `Diets."Id" = 23`, together with its
feeding rounds and hive links. Written because the API refuses this delete:
`DietService.DeleteAsync` only allows a programme to be removed **before** it
has started and while it has no completed rounds, so a programme that is
already under way can only be removed in SQL.

> **Not a template.** These scripts name Id 23 in three files. If you need to
> delete a different programme, copy the folder and change the Id in all three
> (`\set diet_id` in `01`/`03`, the `target_id CONSTANT` in `02`) — do not
> half-edit these.

## What gets deleted, and what does not

| Table | What happens | Why |
|---|---|---|
| `Diets` | the row is deleted | the point of the exercise |
| `FeedingEntries` | all rounds of that diet deleted | `ON DELETE CASCADE` in `DietConfiguration` |
| `DietBeehives` | all hive links deleted | `ON DELETE CASCADE` in `DietConfiguration` |
| `ExpenseItems` | **kept**; only `DietId` set to `NULL` | `ON DELETE SET NULL` — an expense is money that actually left the account, and it must survive the programme it paid for (see the doc comment on `ExpenseItem.DietId`) |
| `Notifications` | **untouched** | no FK, the app never navigates by `RelatedEntityType`, and they record what was actually sent at the time |

`02_delete.sql` never writes any of the cascades by hand — the schema already
states them, and restating them in SQL is how the two drift apart. It asserts
afterwards that the database did all four things, and rolls back if not.

Losing a cost attribution is the one effect that leaves no trace afterwards
(an unlinked expense line looks exactly like one that never had a diet). So
`02_delete.sql` records the affected rows in
`staging.deleted_diet_expense_links` **before** deleting, and `03_verify.sql`
checks each one survived. That table is also the only way to re-attribute
those costs later — export it before dropping it.

## Running it

On the server (`melarium@melarium.app`, `/opt/melarium`):

```bash
git pull
```

### 1. Back up

```bash
docker compose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > ~/melarium-before-delete-diet-23.dump
```

```bash
ls -lh ~/melarium-before-delete-diet-23.dump && pg_restore -l ~/melarium-before-delete-diet-23.dump | head
```

### 2. Copy the scripts into the postgres container

```bash
docker compose cp deploy/data-fixes/delete-diet-23 postgres:/tmp/delete-diet-23
```

### 3. Preview — read this, do not skip it

```bash
docker compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /tmp/delete-diet-23/01_preview.sql'
```

Read only, deletes nothing. Section 1 prints the programme's **name, apiary
and organization** — confirm by eye that this is the programme you mean before
going further. An Id is not a name, and nothing after this point will ask you
again. Sections 2–4 show what goes with it and what survives.

### 4. Delete

One transaction; any failure rolls the whole thing back, and it aborts if
Diet 23 no longer exists rather than silently doing nothing.

```bash
docker compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -f /tmp/delete-diet-23/02_delete.sql'
```

Read the `NOTICE` lines. If the programme had already started or had completed
rounds you will also get a `WARNING` — that is the API's own rule being
bypassed on purpose, and the run continues.

### 5. Verify

```bash
docker compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /tmp/delete-diet-23/03_verify.sql'
```

Sections 1–3 must return **zero rows** and section 6 must be all zeroes.
Section 4 must show `kept = true` and `diet_id` NULL for every expense line
that section 4 of the preview listed. Then open the app: the programme is gone
from **Prehrana**, and from the calendar and any hive that was on it.

### 6. Export the audit trail, then clean up

Only if the preview showed expense lines — otherwise the table is empty and
there is nothing to keep.

```bash
docker compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "\copy staging.deleted_diet_expense_links TO STDOUT WITH CSV HEADER"' > ~/melarium-deleted-diet-23-expense-links.csv
```

Keep the CSV and the dump on the server, same role as the July import's
`melarium-id_map.csv` — the only trace of which expense paid for the deleted
programme. Leave the `staging` table in place unless you have the CSV.

## Optional — remove the "Hranjenje kasni" notifications

Not part of `02_delete.sql`, deliberately: a notification is a record of
something that really was sent, and the app never follows that pointer, so a
dangling one is harmless. If you want them gone from the bell anyway:

```bash
docker compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -f /tmp/delete-diet-23/99_notifications_optional.sql'
```

It refuses to run while Diet 23 still exists, so it cannot clear the alerts of
a live programme by mistake.

## Rollback

A *failed* run needs no rollback — it is one transaction, so nothing was
written. Reverting a *successful* run means restoring the pre-delete dump:

```bash
docker compose stop api
```

```bash
docker compose exec -T postgres sh -c 'pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists' < ~/melarium-before-delete-diet-23.dump
```

```bash
docker compose start api
```

Note this restores the **whole database** to the moment before the delete —
anything else written since is lost with it. Do not leave a long gap between
the delete and deciding whether to keep it.
