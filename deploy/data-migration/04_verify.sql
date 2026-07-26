-- =============================================================================
-- 04_verify.sql — Read-only. Safe to run at any time, before or after cleanup.
--
-- Confirms the imported tree hangs together and that nothing outside the two
-- organizations was touched. Written against organization names rather than
-- staging.id_map so it still works once the staging schema is gone.
-- =============================================================================

\set ON_ERROR_STOP on

\echo ''
\echo '=== 1. Imported organizations ==='
SELECT o."Id", o."Name", o."Plan", o."PlanValidUntil", o."CreatedAt",
       u."Email" AS created_by
FROM "Organizations" o
LEFT JOIN "Users" u ON u."Id" = o."CreatedById"
WHERE o."Name" IN ('AS Honey House', 'Ćatić-Mekić family')
ORDER BY o."Id";

\echo ''
\echo '=== 2. Row counts per organization ==='
SELECT o."Name",
       (SELECT count(*) FROM "Users" u WHERE u."OrganizationId" = o."Id")            AS users,
       (SELECT count(*) FROM "Apiaries" a WHERE a."OrganizationId" = o."Id")         AS apiaries,
       (SELECT count(*) FROM "Beehives" b
          JOIN "Apiaries" a ON a."Id" = b."ApiaryId"
         WHERE a."OrganizationId" = o."Id")                                          AS beehives,
       (SELECT count(*) FROM "Inspections" i
          JOIN "Beehives" b ON b."Id" = i."BeehiveId"
          JOIN "Apiaries" a ON a."Id" = b."ApiaryId"
         WHERE a."OrganizationId" = o."Id")                                          AS inspections,
       (SELECT count(*) FROM "Queens" q
          JOIN "Beehives" b ON b."Id" = q."BeehiveId"
          JOIN "Apiaries" a ON a."Id" = b."ApiaryId"
         WHERE a."OrganizationId" = o."Id")                                          AS queens,
       (SELECT count(*) FROM "Diets" d
          JOIN "Beehives" b ON b."Id" = d."BeehiveId"
          JOIN "Apiaries" a ON a."Id" = b."ApiaryId"
         WHERE a."OrganizationId" = o."Id")                                          AS diets,
       (SELECT count(*) FROM "FeedingEntries" fe
          JOIN "Diets" d    ON d."Id" = fe."DietId"
          JOIN "Beehives" b ON b."Id" = d."BeehiveId"
          JOIN "Apiaries" a ON a."Id" = b."ApiaryId"
         WHERE a."OrganizationId" = o."Id")                                          AS feeding_entries
FROM "Organizations" o
WHERE o."Name" IN ('AS Honey House', 'Ćatić-Mekić family')
ORDER BY o."Name";

\echo ''
\echo '=== 3. Imported users (EmailVerifiedAt must not be null) ==='
SELECT u."Id", u."Email", u."Role", u."OrganizationId", u."ApiaryId",
       u."EmailVerifiedAt" IS NOT NULL AS verified,
       left(u."PasswordHash", 7) AS hash_prefix
FROM "Users" u
JOIN "Organizations" o ON o."Id" = u."OrganizationId"
WHERE o."Name" IN ('AS Honey House', 'Ćatić-Mekić family')
ORDER BY u."Id";

\echo ''
\echo '=== 4. Beehives per apiary, with queen and QR state ==='
SELECT a."Name" AS apiary, b."Id", b."Name", b."LabelNumber",
       b."UniqueId" IS NOT NULL     AS has_unique_id,
       b."QrCodeBase64" IS NOT NULL AS has_qr,
       (SELECT count(*) FROM "Queens" q WHERE q."BeehiveId" = b."Id" AND q."Status" = 1) AS active_queens,
       (SELECT count(*) FROM "Inspections" i WHERE i."BeehiveId" = b."Id")               AS inspections
FROM "Beehives" b
JOIN "Apiaries" a      ON a."Id" = b."ApiaryId"
JOIN "Organizations" o ON o."Id" = a."OrganizationId"
WHERE o."Name" IN ('AS Honey House', 'Ćatić-Mekić family')
ORDER BY a."Name", b."Id";

\echo ''
\echo '=== 5. Integrity: every count below must be 0 ==='
SELECT 'orphan beehives (no apiary)'          AS check_name,
       count(*) AS bad FROM "Beehives" b  LEFT JOIN "Apiaries" a ON a."Id" = b."ApiaryId"  WHERE a."Id" IS NULL
UNION ALL SELECT 'orphan inspections',    count(*) FROM "Inspections" i LEFT JOIN "Beehives" b ON b."Id" = i."BeehiveId" WHERE b."Id" IS NULL
UNION ALL SELECT 'orphan queens',         count(*) FROM "Queens" q      LEFT JOIN "Beehives" b ON b."Id" = q."BeehiveId" WHERE b."Id" IS NULL
UNION ALL SELECT 'orphan diets',          count(*) FROM "Diets" d       LEFT JOIN "Beehives" b ON b."Id" = d."BeehiveId" WHERE b."Id" IS NULL
UNION ALL SELECT 'orphan feeding entries',count(*) FROM "FeedingEntries" fe LEFT JOIN "Diets" d ON d."Id" = fe."DietId" WHERE d."Id" IS NULL
UNION ALL SELECT 'orphan apiaries',       count(*) FROM "Apiaries" a    LEFT JOIN "Organizations" o ON o."Id" = a."OrganizationId" WHERE o."Id" IS NULL
UNION ALL SELECT 'duplicate e-mails',     count(*) FROM (SELECT lower("Email") e FROM "Users" GROUP BY 1 HAVING count(*) > 1) x
UNION ALL SELECT 'duplicate hive UniqueId',count(*) FROM (SELECT "UniqueId" FROM "Beehives" WHERE "UniqueId" IS NOT NULL GROUP BY 1 HAVING count(*) > 1) x
UNION ALL SELECT 'todos with both/neither FK', count(*) FROM "Todos"
    WHERE ("ApiaryId" IS NULL) = ("BeehiveId" IS NULL)
UNION ALL SELECT 'hives with >1 active queen', count(*) FROM (
    SELECT "BeehiveId" FROM "Queens" WHERE "Status" = 1 GROUP BY 1 HAVING count(*) > 1) x;

\echo ''
\echo '=== 6. Identity sequences vs. MAX(Id) ==='
-- next_id must be greater than max_id on every row. If it is not, the next insert
-- made by the application would collide with an existing primary key.
CREATE OR REPLACE FUNCTION pg_temp.seq_report()
RETURNS TABLE (table_name text, max_id bigint, next_id bigint, ok boolean)
LANGUAGE plpgsql AS $$
DECLARE t text; seq text; last_val bigint; called boolean;
BEGIN
    FOREACH t IN ARRAY ARRAY['Organizations','Users','Apiaries','Beehives','Inspections',
                             'Queens','Diets','FeedingEntries','Todos','Treatments',
                             'TreatmentEntries','LearningTopics']
    LOOP
        seq := pg_get_serial_sequence(format('%I', t), 'Id');
        EXECUTE format('SELECT COALESCE(MAX("Id"), 0) FROM %I', t) INTO max_id;
        EXECUTE format('SELECT last_value, is_called FROM %s', seq) INTO last_val, called;
        table_name := t;
        next_id    := CASE WHEN called THEN last_val + 1 ELSE last_val END;
        ok         := next_id > max_id;
        RETURN NEXT;
    END LOOP;
END $$;

SELECT * FROM pg_temp.seq_report() ORDER BY ok, table_name;
