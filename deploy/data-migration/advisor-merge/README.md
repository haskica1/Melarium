# One-off data migration — AI Savjetnik history into AI Asistent (SPEC-18)

Copies every `AdvisorConversations`/`AdvisorMessages` row into the unified
`AiAssistantSessions`/`AiAssistantTurns` shape, so a beekeeper's old
Savjetnik conversations show up in the merged Asistent's history instead of
disappearing when `/advisor` is retired. Migrated turns carry **zero**
proposed actions — under SPEC-18 that is correct: the advisor only ever
answered questions, so "no actions" is the honest fact for that turn, not a
gap in the migration.

This is independent of the July 2026 old-database import in the parent
folder — different source, different target, kept in its own subfolder so
the two are never confused.

## Prerequisites

- The backend deploy that adds `AiAssistantSession.BeehiveId` (migration
  `AddAiAssistantSessionBeehive`) must already be applied — `01_backfill.sql`
  writes into that column. It ships with SPEC-18's application code, ahead
  of this script.
- A verified backup (below).

## Running it

From the repo root, on the server (`melarium@melarium.app`, `/opt/melarium`):

```bash
git pull
```

### 1. Back up

```bash
docker compose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > ~/melarium-before-advisor-merge.dump
```

```bash
ls -lh ~/melarium-before-advisor-merge.dump && pg_restore -l ~/melarium-before-advisor-merge.dump | head
```

### 2. Copy the scripts into the postgres container

```bash
docker compose cp deploy/data-migration/advisor-merge postgres:/tmp/advisor-merge
```

### 3. Backfill

One transaction; any failure rolls the whole thing back, and it refuses to
run a second time (see the pre-flight check in `01_backfill.sql`).

```bash
docker compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -f /tmp/advisor-merge/01_backfill.sql'
```

Read the `NOTICE` line it prints — it states how many sessions and turns were
migrated. Compare that by eye against how many Advisor conversations you
expect to exist (there is no `expected_counts` pin here, unlike the July
import — this migration is a straight copy, not a curated subset, so the
row counts are whatever `AdvisorConversations` already holds).

### 4. Verify

```bash
docker compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /tmp/advisor-merge/02_verify.sql'
```

Section 1's two pairs of counts must match; sections 2–4 must return **zero
rows**. Section 5 is a spot-check list — pick one or two of the sessions it
prints, log in as that user, open `/assistant`, and confirm the conversation
reads correctly with no proposal cards.

### 5. Export the audit trail, then clean up

```bash
docker compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "\copy staging.advisor_merge_id_map TO STDOUT WITH CSV HEADER"' > ~/melarium-advisor-merge-id_map.csv
```

```bash
docker compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "DROP TABLE staging.advisor_merge_id_map;"'
```

Keep `~/melarium-advisor-merge-id_map.csv` and
`~/melarium-before-advisor-merge.dump` on the server — same role as the July
import's `melarium-id_map.csv`/`melarium-before-import.dump`: the only way
to trace a migrated session back to its original Advisor conversation, or to
undo this.

## What this does **not** do

**Does not drop `AdvisorConversations`/`AdvisorMessages`.** Those tables,
their entities, and their EF configuration stay exactly as they are after
this script runs — dropping them is a separate, later deploy (SPEC-18
"Migration B"), done only once the backfill above has been confirmed correct
on production. Never run in the same sitting as the backfill: if something
about the copy turns out wrong, you want the source rows still there to
re-derive from, not already gone.

## Rollback

Same shape as the July import: the backfill is one transaction, so a
*failed* run needs no rollback — nothing was written. Reverting a
*successful* run means restoring the pre-backfill dump:

```bash
docker compose stop api
```

```bash
docker compose exec -T postgres sh -c 'pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists' < ~/melarium-before-advisor-merge.dump
```

```bash
docker compose start api
```
