-- =============================================================================
-- 03_verify.sql — READ ONLY. Run after 02_delete.sql.
--
-- Sections 1–3 and 6 must come back empty / all zeroes. Section 4 is the
-- opposite: every row it lists must say kept = true and diet_id = NULL.
-- Section 5 is informational.
-- =============================================================================

\set ON_ERROR_STOP on
\set diet_id 23

\echo ''
\echo '=== 1. The programme is gone (expect 0 rows) ==============================='

SELECT "Id", "Name" FROM "Diets" WHERE "Id" = :diet_id;

\echo ''
\echo '=== 2. No feeding rounds left behind (expect 0 rows) ======================='

SELECT "Id", "DietId", "ScheduledDate"::date
FROM "FeedingEntries" WHERE "DietId" = :diet_id;

\echo ''
\echo '=== 3. No hive links left behind (expect 0 rows) =========================='

SELECT "Id", "DietId", "BeehiveId"
FROM "DietBeehives" WHERE "DietId" = :diet_id;

\echo ''
\echo '=== 4. Every expense line survived, unlinked =============================='
\echo '    (kept must be true and diet_id NULL on every row. No rows at all is'
\echo '     correct ONLY if section 4 of 01_preview.sql was also empty.)'

SELECT l.expense_item_id,
       l.expense_id,
       l.item_name,
       l.total_price,
       (ei."Id" IS NOT NULL) AS kept,
       ei."DietId"           AS diet_id
FROM staging.deleted_diet_expense_links l
LEFT JOIN "ExpenseItems" ei ON ei."Id" = l.expense_item_id
WHERE l.diet_id = :diet_id
ORDER BY l.expense_item_id;

\echo ''
\echo '=== 5. Notifications left in place (informational, not an error) =========='

SELECT "Id", "UserId", "Title", "CreatedAt"
FROM "Notifications"
WHERE "RelatedEntityType" = 'Diet' AND "RelatedEntityId" = :diet_id
ORDER BY "Id";

\echo ''
\echo '=== 6. Nothing anywhere still points at the diet (expect all 0) ==========='

SELECT (SELECT count(*) FROM "Diets"          WHERE "Id"     = :diet_id) AS diets,
       (SELECT count(*) FROM "FeedingEntries" WHERE "DietId" = :diet_id) AS feeding_rounds,
       (SELECT count(*) FROM "DietBeehives"   WHERE "DietId" = :diet_id) AS hive_links,
       (SELECT count(*) FROM "ExpenseItems"   WHERE "DietId" = :diet_id) AS expense_lines_still_linked,
       (SELECT count(*) FROM staging.deleted_diet_expense_links l
          LEFT JOIN "ExpenseItems" ei ON ei."Id" = l.expense_item_id
         WHERE l.diet_id = :diet_id AND ei."Id" IS NULL) AS expense_lines_lost;

\echo ''
