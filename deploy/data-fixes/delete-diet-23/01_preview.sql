-- =============================================================================
-- 01_preview.sql — READ ONLY. Shows exactly what deleting feeding programme
-- (Diet) 23 would remove, and what it would leave behind.
--
-- Deletes nothing. Run this first and read every section before 02_delete.sql:
-- an Id is not a name, and the only thing standing between "the right diet" and
-- "someone else's diet" is you reading section 1 and recognising it.
-- =============================================================================

\set ON_ERROR_STOP on
\set diet_id 23

\echo ''
\echo '=== 1. The programme itself (must be the one you mean) ======================'

SELECT d."Id",
       d."Name",
       o."Name"  AS organization,
       a."Name"  AS apiary,
       CASE d."Status" WHEN 1 THEN 'NotStarted' WHEN 2 THEN 'InProgress'
                       WHEN 3 THEN 'Completed'  WHEN 4 THEN 'StoppedEarly'
                       ELSE d."Status"::text END AS status,
       d."StartDate"::date       AS start_date,
       d."DurationDays"          AS duration_days,
       d."FrequencyDays"         AS frequency_days,
       d."CreatedAt"             AS created_at,
       u."FirstName" || ' ' || u."LastName" AS created_by
FROM "Diets" d
JOIN "Apiaries"      a ON a."Id" = d."ApiaryId"
JOIN "Organizations" o ON o."Id" = a."OrganizationId"
LEFT JOIN "Users"    u ON u."Id" = d."CreatedById"
WHERE d."Id" = :diet_id;

\echo ''
\echo '=== 2. Feeding rounds that go with it (CASCADE — all deleted) =============='

SELECT e."Id",
       e."ScheduledDate"::date AS scheduled,
       CASE e."Status" WHEN 1 THEN 'Pending' WHEN 2 THEN 'Completed'
                       ELSE e."Status"::text END AS status,
       e."CompletionDate"::date AS completed_on,
       e."Note"
FROM "FeedingEntries" e
WHERE e."DietId" = :diet_id
ORDER BY e."ScheduledDate";

\echo ''
\echo '=== 3. Hive links that go with it (CASCADE — all deleted) =================='

SELECT db."Id",
       b."Id"          AS beehive_id,
       b."LabelNumber",
       b."Name"        AS beehive_name,
       db."RemovedOn"::date AS removed_on
FROM "DietBeehives" db
JOIN "Beehives" b ON b."Id" = db."BeehiveId"
WHERE db."DietId" = :diet_id
ORDER BY b."LabelNumber" NULLS LAST, b."Id";

\echo ''
\echo '=== 4. Expense lines attributed to it (KEPT — only unlinked, SET NULL) ====='

SELECT ei."Id"       AS expense_item_id,
       ei."ExpenseId",
       ei."Name",
       ei."Quantity",
       ei."Unit",
       ei."TotalPrice",
       ex."PurchaseDate"::date AS purchase_date
FROM "ExpenseItems" ei
JOIN "Expenses" ex ON ex."Id" = ei."ExpenseId"
WHERE ei."DietId" = :diet_id
ORDER BY ei."Id";

\echo ''
\echo '=== 5. Notifications pointing at it (LEFT ALONE by 02_delete.sql) =========='

SELECT n."Id", n."UserId", n."Title", n."IsRead", n."CreatedAt"
FROM "Notifications" n
WHERE n."RelatedEntityType" = 'Diet' AND n."RelatedEntityId" = :diet_id
ORDER BY n."Id";

\echo ''
\echo '=== 6. Summary ============================================================='

SELECT (SELECT count(*) FROM "Diets"          WHERE "Id"     = :diet_id) AS diets,
       (SELECT count(*) FROM "FeedingEntries" WHERE "DietId" = :diet_id) AS feeding_rounds,
       (SELECT count(*) FROM "FeedingEntries" WHERE "DietId" = :diet_id AND "Status" = 2) AS rounds_completed,
       (SELECT count(*) FROM "DietBeehives"   WHERE "DietId" = :diet_id) AS hive_links,
       (SELECT count(*) FROM "ExpenseItems"   WHERE "DietId" = :diet_id) AS expense_lines_unlinked,
       (SELECT count(*) FROM "Notifications"
         WHERE "RelatedEntityType" = 'Diet' AND "RelatedEntityId" = :diet_id) AS notifications_left;

\echo ''
