-- =============================================================================
-- 02_delete.sql — Deletes feeding programme (Diet) 23 and everything the schema
-- says belongs to it.
--
-- ONE transaction: either the programme, its rounds and its hive links all go,
-- or nothing does. Run 01_preview.sql first and recognise the programme it
-- prints — this script finds its target by Id alone, and an Id is not a name.
--
-- What the delete does, and why the script does not spell any of it out in SQL:
--   "FeedingEntries"  → deleted by ON DELETE CASCADE  (DietConfiguration)
--   "DietBeehives"    → deleted by ON DELETE CASCADE  (DietConfiguration)
--   "ExpenseItems"    → "DietId" set to NULL by ON DELETE SET NULL; the expense
--                       line SURVIVES. That is deliberate and documented on
--                       ExpenseItem.DietId: an expense records money that
--                       actually left the account, and deleting a feeding
--                       programme must never delete it.
-- Doing those by hand would re-state the schema's rules in a second place,
-- where they could drift out of step with the entity configuration. Instead the
-- verification block below asserts the database really did all four things
-- before the transaction is allowed to commit.
--
-- The unlinking is the one destructive effect that leaves no trace: afterwards
-- an expense line that lost its attribution looks exactly like one that never
-- had it. So the affected rows are recorded first, in
-- staging.deleted_diet_expense_links — that table is what 03_verify.sql checks
-- against, and the only way to re-attribute those costs later.
--
-- "Notifications" rows with RelatedEntityType='Diet' are NOT touched — there is
-- no FK, the app never navigates by that pointer, and they are a record of what
-- was sent at the time. See the README for a one-liner if you want them gone.
--
-- NOTE: this bypasses the API's own rule (DietService.DeleteAsync refuses a
-- programme that has started or has completed rounds). That is the whole point
-- of doing it in SQL — but it means the guard is now your eyes on the preview.
-- The block below raises a WARNING for exactly that case and still proceeds.
--
-- Not idempotent, and does not need to be: a second run finds no Diet 23 and
-- aborts on the pre-flight check rather than silently doing nothing.
-- =============================================================================

\set ON_ERROR_STOP on

BEGIN;

-- The Id lives in exactly one place: the CONSTANT below. psql's \set is
-- deliberately not used for it — psql does not interpolate :variables inside
-- dollar-quoted blocks, so a \set here would silently fail to apply.

DO $$
DECLARE
    target_id  CONSTANT int := 23;

    d_name        text;
    d_apiary      text;
    d_org         text;
    d_status      int;
    n_rounds      int;
    n_completed   int;
    n_hives       int;
    n_expenses    int;
    n_notif       int;

    after_diet     int;
    after_rounds   int;
    after_hives    int;
    after_linked   int;
    n_recorded     int;
    n_lost         int;
BEGIN
    -- ─── Pre-flight: the programme must exist ────────────────────────────────

    SELECT d."Name", a."Name", o."Name", d."Status"
      INTO d_name, d_apiary, d_org, d_status
    FROM "Diets" d
    JOIN "Apiaries"      a ON a."Id" = d."ApiaryId"
    JOIN "Organizations" o ON o."Id" = a."OrganizationId"
    WHERE d."Id" = target_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION
            'No Diet with Id % exists — nothing to delete. (Already deleted, or the Id is wrong: check 01_preview.sql section 1.)',
            target_id;
    END IF;

    SELECT count(*), count(*) FILTER (WHERE "Status" = 2)
      INTO n_rounds, n_completed
    FROM "FeedingEntries" WHERE "DietId" = target_id;

    SELECT count(*) INTO n_hives    FROM "DietBeehives" WHERE "DietId" = target_id;
    SELECT count(*) INTO n_expenses FROM "ExpenseItems" WHERE "DietId" = target_id;
    SELECT count(*) INTO n_notif    FROM "Notifications"
        WHERE "RelatedEntityType" = 'Diet' AND "RelatedEntityId" = target_id;

    RAISE NOTICE 'Deleting Diet % — "%" (apiary "%", organization "%").',
        target_id, d_name, d_apiary, d_org;
    RAISE NOTICE '  % feeding round(s), of which % completed', n_rounds, n_completed;
    RAISE NOTICE '  % hive link(s)', n_hives;
    RAISE NOTICE '  % expense line(s) will be UNLINKED, not deleted', n_expenses;
    RAISE NOTICE '  % notification(s) left untouched', n_notif;

    IF n_completed > 0 OR d_status IN (2, 3, 4) THEN
        RAISE WARNING
            'Diet % has already started (status %) and/or has % completed round(s). The API would refuse to delete it; this script proceeds because it was run deliberately. The field history of those rounds is being destroyed.',
            target_id, d_status, n_completed;
    END IF;

    -- ─── Record the cost attributions about to be lost ───────────────────────
    -- Table is generic (diet_id is a column, not part of the name) so a future
    -- deletion of a different programme appends here instead of inventing a
    -- second table shape. DDL is transactional in Postgres: if anything below
    -- fails, this table and its rows roll back with everything else.

    CREATE SCHEMA IF NOT EXISTS staging;

    CREATE TABLE IF NOT EXISTS staging.deleted_diet_expense_links (
        diet_id         int  NOT NULL,
        expense_item_id int  NOT NULL,
        expense_id      int  NOT NULL,
        item_name       text,
        total_price     numeric,
        unlinked_at     timestamptz NOT NULL DEFAULT now(),
        PRIMARY KEY (diet_id, expense_item_id)
    );

    COMMENT ON TABLE staging.deleted_diet_expense_links IS
        'ExpenseItems that lost their DietId when a feeding programme was hard-deleted (deploy/data-fixes). The only record of which expense paid for which deleted programme — export before dropping.';

    INSERT INTO staging.deleted_diet_expense_links
        (diet_id, expense_item_id, expense_id, item_name, total_price)
    SELECT target_id, ei."Id", ei."ExpenseId", ei."Name", ei."TotalPrice"
    FROM "ExpenseItems" ei
    WHERE ei."DietId" = target_id
    ON CONFLICT (diet_id, expense_item_id) DO NOTHING;

    SELECT count(*) INTO n_recorded
    FROM staging.deleted_diet_expense_links WHERE diet_id = target_id;

    IF n_recorded <> n_expenses THEN
        RAISE EXCEPTION
            'Recorded % expense link(s) but % are attributed to Diet % — refusing to delete with an incomplete audit trail.',
            n_recorded, n_expenses, target_id;
    END IF;

    -- ─── Delete ──────────────────────────────────────────────────────────────

    DELETE FROM "Diets" WHERE "Id" = target_id;

    -- ─── Verify the cascades actually fired, before COMMIT ───────────────────

    SELECT count(*) INTO after_diet   FROM "Diets"          WHERE "Id"     = target_id;
    SELECT count(*) INTO after_rounds FROM "FeedingEntries" WHERE "DietId" = target_id;
    SELECT count(*) INTO after_hives  FROM "DietBeehives"   WHERE "DietId" = target_id;
    SELECT count(*) INTO after_linked FROM "ExpenseItems"   WHERE "DietId" = target_id;

    IF after_diet <> 0 THEN
        RAISE EXCEPTION 'Diet % still present after DELETE.', target_id;
    END IF;
    IF after_rounds <> 0 THEN
        RAISE EXCEPTION '% feeding round(s) still reference Diet % — cascade did not fire.', after_rounds, target_id;
    END IF;
    IF after_hives <> 0 THEN
        RAISE EXCEPTION '% hive link(s) still reference Diet % — cascade did not fire.', after_hives, target_id;
    END IF;
    IF after_linked <> 0 THEN
        RAISE EXCEPTION '% expense line(s) still reference Diet % — SET NULL did not fire.', after_linked, target_id;
    END IF;

    -- Every recorded expense line must still exist. SET NULL must have unlinked
    -- them, not cascaded them away.
    SELECT count(*) INTO n_lost
    FROM staging.deleted_diet_expense_links l
    LEFT JOIN "ExpenseItems" ei ON ei."Id" = l.expense_item_id
    WHERE l.diet_id = target_id AND ei."Id" IS NULL;

    IF n_lost <> 0 THEN
        RAISE EXCEPTION
            '% expense line(s) disappeared with the diet — they must survive. Rolling back.',
            n_lost;
    END IF;

    RAISE NOTICE 'Done: Diet % deleted, % round(s) and % hive link(s) removed with it, % expense line(s) kept and unlinked (recorded in staging.deleted_diet_expense_links).',
        target_id, n_rounds, n_hives, n_expenses;
END $$;

COMMIT;
