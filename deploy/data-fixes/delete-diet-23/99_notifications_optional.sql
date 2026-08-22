-- =============================================================================
-- 99_notifications_optional.sql — OPTIONAL, and deliberately not part of
-- 02_delete.sql.
--
-- Deletes the "Hranjenje kasni" notifications that point at Diet 23
-- (RelatedEntityType='Diet', RelatedEntityId=23).
--
-- Why it is optional: there is no foreign key here, and the app never navigates
-- by that pointer — the frontend only carries RelatedEntityType through to the
-- DTO. So a notification about a deleted programme is harmless text, and it is
-- also a true record of something that really was sent to that user at that
-- time. Deleting it rewrites that record. Run this only if you would rather
-- the old alerts disappeared from the bell.
--
-- Run AFTER 02_delete.sql. Refuses to run while Diet 23 still exists, so it can
-- never delete the alerts of a live programme.
-- =============================================================================

\set ON_ERROR_STOP on

BEGIN;

DO $$
DECLARE
    target_id CONSTANT int := 23;
    n_deleted int;
BEGIN
    IF EXISTS (SELECT 1 FROM "Diets" WHERE "Id" = target_id) THEN
        RAISE EXCEPTION
            'Diet % still exists — these notifications belong to a live programme. Run 02_delete.sql first, or do not run this at all.',
            target_id;
    END IF;

    DELETE FROM "Notifications"
    WHERE "RelatedEntityType" = 'Diet' AND "RelatedEntityId" = target_id;

    GET DIAGNOSTICS n_deleted = ROW_COUNT;

    RAISE NOTICE 'Deleted % notification(s) pointing at Diet %.', n_deleted, target_id;
END $$;

COMMIT;
