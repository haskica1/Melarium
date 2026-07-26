-- =============================================================================
-- 02_import.sql — Import the selected old-production data into the live tables.
--
-- Runs as ONE transaction. Any failed check aborts the whole thing and leaves
-- the database exactly as it was.
--
-- ID POLICY — no old Id is ever reused.
--   Every row gets a fresh Id drawn from the table's own identity sequence with
--   nextval(), recorded in staging.id_map(entity, old_id, new_id). All foreign
--   keys are translated through that map. This is why the script cannot collide
--   with rows that already exist in production, and why the sequences are
--   correct afterwards without any setval() fix-up at the end.
--
--   Side note, and the reason sync_seq() exists at all: the seeded rows in
--   Organizations/Apiaries/Beehives/Inspections (Ids 1–4, inserted by
--   InitialCreate via InsertData) never advanced their identity sequences. On a
--   database where nothing has been created through the app yet, nextval() would
--   hand out Id 1 and hit the seeded row. sync_seq() lifts each sequence past
--   MAX(Id) before allocating — which also repairs that latent problem for the
--   application itself.
--
-- NOT TRANSACTIONAL: sequence values. A rolled-back run still leaves the
-- sequences advanced. Harmless (gaps in Ids only), and re-running is safe.
-- =============================================================================

\set ON_ERROR_STOP on

BEGIN;

-- ─── Expected row counts ─────────────────────────────────────────────────────
-- Derived from the CSV exports as reviewed. If a re-export changes the data,
-- these numbers must be updated deliberately — the script refuses to guess.

CREATE TEMP TABLE expected_counts (entity text PRIMARY KEY, n int) ON COMMIT DROP;
INSERT INTO expected_counts VALUES
    ('Organizations',     2),   -- AS Honey House, Ćatić-Mekić family
    ('Users',             3),   -- Asim Haskić, Amel Ćatić, Semin Alihodžić
    ('Apiaries',          2),   -- Dolac Vakuf (org 6), Crniče (org 7)
    ('Beehives',         13),   -- 6 in Dolac Vakuf, 7 in Crniče
    ('Inspections',       9),
    ('Queens',            6),
    ('Diets',             6),
    ('FeedingEntries',   30),
    ('Todos',             2),
    ('Treatments',        0),   -- every treatment in the export belongs to a test org
    ('TreatmentEntries',  0);

-- ─── Helpers ─────────────────────────────────────────────────────────────────

DROP TABLE IF EXISTS staging.id_map;
CREATE TABLE staging.id_map (
    entity text NOT NULL,
    old_id int  NOT NULL,
    new_id int  NOT NULL,
    PRIMARY KEY (entity, old_id),
    UNIQUE (entity, new_id)
);
COMMENT ON TABLE staging.id_map IS
    'old-database Id -> new Id, per table. Audit trail for the CSV import; export before dropping the staging schema.';

CREATE OR REPLACE FUNCTION staging.seq_for(p_table text) RETURNS text
LANGUAGE sql AS $$ SELECT pg_get_serial_sequence(format('%I', p_table), 'Id') $$;

CREATE OR REPLACE FUNCTION staging.sync_seq(p_table text) RETURNS bigint
LANGUAGE plpgsql AS $$
DECLARE seq text; maxid bigint;
BEGIN
    seq := staging.seq_for(p_table);
    IF seq IS NULL THEN
        RAISE EXCEPTION 'No identity sequence found for %."Id"', p_table;
    END IF;
    EXECUTE format('SELECT COALESCE(MAX("Id"), 0) FROM %I', p_table) INTO maxid;
    -- Only ever raise the sequence, never lower it: nextval() covers the case
    -- where the sequence is already ahead of MAX(Id) because rows were deleted.
    RETURN setval(seq, GREATEST(maxid, nextval(seq)));
END $$;

CREATE OR REPLACE FUNCTION staging.alloc_ids(p_entity text, p_view text) RETURNS int
LANGUAGE plpgsql AS $$
DECLARE seq text; n int;
BEGIN
    PERFORM staging.sync_seq(p_entity);
    seq := staging.seq_for(p_entity);
    EXECUTE format($q$
        INSERT INTO staging.id_map (entity, old_id, new_id)
        SELECT %L, t.old_id, nextval(%L)
        FROM (SELECT "Id"::int AS old_id FROM staging.%I ORDER BY 1) t
    $q$, p_entity, seq, p_view);
    GET DIAGNOSTICS n = ROW_COUNT;
    RETURN n;
END $$;

-- =============================================================================
-- PRE-FLIGHT CHECKS — all of these run before a single row is written.
-- =============================================================================

-- 1. The selection must match what was reviewed.
DO $$
DECLARE r record; actual int;
BEGIN
    FOR r IN SELECT entity, n FROM expected_counts LOOP
        EXECUTE format('SELECT count(*) FROM staging.sel_%s', lower(r.entity)) INTO actual;
        IF actual <> r.n THEN
            RAISE EXCEPTION
                'ABORTED: % selected % row(s), expected %. The CSV exports differ from the reviewed set — check staging.included_org and the expected_counts table above.',
                r.entity, actual, r.n;
        END IF;
    END LOOP;
END $$;

-- 2. No e-mail may already exist in the target database ("Users"."Email" is unique).
--    A hit here means the account was re-created on the new production and this
--    import would need a merge decision instead of an insert.
DO $$
DECLARE clashes text;
BEGIN
    SELECT string_agg(u."Email", ', ' ORDER BY u."Email") INTO clashes
    FROM staging.sel_users s
    JOIN "Users" u ON u."Email" = lower(trim(s."Email"));
    IF clashes IS NOT NULL THEN
        RAISE EXCEPTION
            'ABORTED: these e-mail addresses already exist in the target database: %. Decide per account whether to merge into the existing user or skip it, then re-run.',
            clashes;
    END IF;
END $$;

-- 3. No hive UniqueId may already exist ("Beehives"."UniqueId" is unique where
--    not null). The GUIDs are carried over verbatim so printed QR codes keep
--    resolving — see the note at the end of this file.
DO $$
DECLARE clashes text;
BEGIN
    SELECT string_agg(b."UniqueId"::text, ', ') INTO clashes
    FROM staging.sel_beehives s
    JOIN "Beehives" b ON b."UniqueId" = s."UniqueId"::uuid
    WHERE s."UniqueId" IS NOT NULL;
    IF clashes IS NOT NULL THEN
        RAISE EXCEPTION 'ABORTED: these beehive UniqueId values already exist in the target database: %.', clashes;
    END IF;
END $$;

-- 4. Pastures and apiary moves are not part of this import, so no selected
--    apiary may currently sit on a pasture.
DO $$
DECLARE offenders text;
BEGIN
    SELECT string_agg("Id", ', ') INTO offenders
    FROM staging.sel_apiaries WHERE "CurrentPastureId" IS NOT NULL;
    IF offenders IS NOT NULL THEN
        RAISE EXCEPTION
            'ABORTED: apiary/apiaries % reference a pasture (CurrentPastureId), but Pastures are not part of this import. Export Pastures + ApiaryMoves as well, or clear the reference.',
            offenders;
    END IF;
END $$;

-- 5. Every user referenced as creator/assignee must itself be in the selection.
--    Without this the script would silently drop authorship (all of these FKs
--    are nullable), which is exactly the kind of quiet data loss to avoid.
DO $$
DECLARE missing text;
BEGIN
    SELECT string_agg(DISTINCT x.ref::text, ', ') INTO missing
    FROM (
        SELECT "CreatedById"::int AS ref FROM staging.sel_organizations WHERE "CreatedById"  IS NOT NULL
        UNION ALL SELECT "CreatedById"::int  FROM staging.sel_apiaries   WHERE "CreatedById"  IS NOT NULL
        UNION ALL SELECT "CreatedById"::int  FROM staging.sel_beehives   WHERE "CreatedById"  IS NOT NULL
        UNION ALL SELECT "CreatedById"::int  FROM staging.sel_diets      WHERE "CreatedById"  IS NOT NULL
        UNION ALL SELECT "CreatedById"::int  FROM staging.sel_treatments WHERE "CreatedById"  IS NOT NULL
        UNION ALL SELECT "CreatedById"::int  FROM staging.sel_todos      WHERE "CreatedById"  IS NOT NULL
        UNION ALL SELECT "AssignedToId"::int FROM staging.sel_todos      WHERE "AssignedToId" IS NOT NULL
    ) x
    WHERE NOT EXISTS (SELECT 1 FROM staging.sel_users u WHERE u."Id"::int = x.ref);
    IF missing IS NOT NULL THEN
        RAISE EXCEPTION
            'ABORTED: old user id(s) % are referenced by selected rows but are not themselves selected. Either add their organization to staging.included_org or decide to null the reference.',
            missing;
    END IF;
END $$;

-- 6. Cross-boundary FK check for the tables whose selection view does not
--    already guarantee it (todos match on either FK; treatment entries and
--    users carry a second FK).
DO $$
DECLARE offenders text;
BEGIN
    SELECT string_agg("Id", ', ') INTO offenders FROM staging.sel_todos
    WHERE ("ApiaryId"  IS NOT NULL AND "ApiaryId"::int  NOT IN (SELECT "Id"::int FROM staging.sel_apiaries))
       OR ("BeehiveId" IS NOT NULL AND "BeehiveId"::int NOT IN (SELECT "Id"::int FROM staging.sel_beehives));
    IF offenders IS NOT NULL THEN
        RAISE EXCEPTION 'ABORTED: todo(s) % point outside the selected apiaries/beehives.', offenders;
    END IF;

    SELECT string_agg("Id", ', ') INTO offenders FROM staging.sel_treatmententries
    WHERE "BeehiveId"::int NOT IN (SELECT "Id"::int FROM staging.sel_beehives);
    IF offenders IS NOT NULL THEN
        RAISE EXCEPTION 'ABORTED: treatment entry/entries % point to a beehive outside the selection.', offenders;
    END IF;

    SELECT string_agg("Id", ', ') INTO offenders FROM staging.sel_users
    WHERE "ApiaryId" IS NOT NULL AND "ApiaryId"::int NOT IN (SELECT "Id"::int FROM staging.sel_apiaries);
    IF offenders IS NOT NULL THEN
        RAISE EXCEPTION 'ABORTED: user(s) % are assigned to an apiary outside the selection.', offenders;
    END IF;
END $$;

-- 7. CreatedAt is NOT NULL on every table (BaseEntity).
DO $$
DECLARE r record; bad int;
BEGIN
    FOR r IN SELECT entity FROM expected_counts LOOP
        EXECUTE format('SELECT count(*) FROM staging.sel_%s WHERE "CreatedAt" IS NULL', lower(r.entity)) INTO bad;
        IF bad > 0 THEN
            RAISE EXCEPTION 'ABORTED: % has % row(s) with an empty CreatedAt.', r.entity, bad;
        END IF;
    END LOOP;
END $$;

\echo 'Pre-flight checks passed.'

-- =============================================================================
-- ID ALLOCATION
-- =============================================================================

SELECT staging.alloc_ids('Organizations',    'sel_organizations')    AS organizations,
       staging.alloc_ids('Users',            'sel_users')            AS users,
       staging.alloc_ids('Apiaries',         'sel_apiaries')         AS apiaries,
       staging.alloc_ids('Beehives',         'sel_beehives')         AS beehives,
       staging.alloc_ids('Inspections',      'sel_inspections')      AS inspections,
       staging.alloc_ids('Queens',           'sel_queens')           AS queens,
       staging.alloc_ids('Diets',            'sel_diets')            AS diets,
       staging.alloc_ids('FeedingEntries',   'sel_feedingentries')   AS feeding_entries,
       staging.alloc_ids('Todos',            'sel_todos')            AS todos,
       staging.alloc_ids('Treatments',       'sel_treatments')       AS treatments,
       staging.alloc_ids('TreatmentEntries', 'sel_treatmententries') AS treatment_entries;

-- =============================================================================
-- INSERTS — parents first.
--
-- Organizations.CreatedById -> Users and Users.ApiaryId -> Apiaries are circular
-- with Users.OrganizationId / Apiaries.CreatedById, so both are inserted as NULL
-- and filled in by an UPDATE once the other side exists.
-- =============================================================================

INSERT INTO "Organizations"
    ("Id", "Name", "Description", "Plan", "PlanValidUntil", "PlanNotes", "CreatedById", "CreatedAt", "UpdatedAt")
SELECT m.new_id,
       s."Name",
       s."Description",
       s."Plan"::int,
       s."PlanValidUntil"::timestamptz,
       s."PlanNotes",
       NULL::int,                              -- filled in after Users exist
       s."CreatedAt"::timestamptz,
       s."UpdatedAt"::timestamptz
FROM staging.sel_organizations s
JOIN staging.id_map m ON m.entity = 'Organizations' AND m.old_id = s."Id"::int;

-- EmailVerifiedAt: the old database predates e-mail verification, so the CSV has
-- no such column. Grandfather these accounts with their sign-up date — the same
-- policy migration 20260725105931_AddUserTokensAndEmailVerification applied to
-- every account that existed when the feature shipped. Leaving it NULL would
-- show a live user a permanent "confirm your address" prompt.
INSERT INTO "Users"
    ("Id", "FirstName", "LastName", "Email", "PasswordHash", "Role",
     "EmailVerifiedAt", "OrganizationId", "ApiaryId", "CreatedAt", "UpdatedAt")
SELECT m.new_id,
       s."FirstName",
       s."LastName",
       lower(trim(s."Email")),                 -- login looks the address up lower-cased
       s."PasswordHash",                        -- BCrypt, same format — passwords keep working
       s."Role"::int,
       s."CreatedAt"::timestamptz,
       om.new_id,
       NULL::int,                              -- filled in after Apiaries exist
       s."CreatedAt"::timestamptz,
       s."UpdatedAt"::timestamptz
FROM staging.sel_users s
JOIN staging.id_map m       ON m.entity  = 'Users'         AND m.old_id  = s."Id"::int
LEFT JOIN staging.id_map om ON om.entity = 'Organizations' AND om.old_id = s."OrganizationId"::int;

UPDATE "Organizations" o
SET "CreatedById" = um.new_id
FROM staging.sel_organizations s
JOIN staging.id_map m   ON m.entity  = 'Organizations' AND m.old_id  = s."Id"::int
JOIN staging.id_map um  ON um.entity = 'Users'         AND um.old_id = s."CreatedById"::int
WHERE o."Id" = m.new_id;

INSERT INTO "Apiaries"
    ("Id", "Name", "Description", "Latitude", "Longitude", "HomeLatitude", "HomeLongitude",
     "OrganizationId", "CurrentPastureId", "CreatedById", "CreatedAt", "UpdatedAt")
SELECT m.new_id,
       s."Name",
       s."Description",
       s."Latitude"::double precision,
       s."Longitude"::double precision,
       s."HomeLatitude"::double precision,
       s."HomeLongitude"::double precision,
       om.new_id,
       NULL::int,                              -- asserted NULL in pre-flight check 4
       um.new_id,
       s."CreatedAt"::timestamptz,
       s."UpdatedAt"::timestamptz
FROM staging.sel_apiaries s
JOIN staging.id_map m       ON m.entity  = 'Apiaries'      AND m.old_id  = s."Id"::int
JOIN staging.id_map om      ON om.entity = 'Organizations' AND om.old_id = s."OrganizationId"::int
LEFT JOIN staging.id_map um ON um.entity = 'Users'         AND um.old_id = s."CreatedById"::int;

UPDATE "Users" u
SET "ApiaryId" = am.new_id
FROM staging.sel_users s
JOIN staging.id_map m  ON m.entity  = 'Users'    AND m.old_id  = s."Id"::int
JOIN staging.id_map am ON am.entity = 'Apiaries' AND am.old_id = s."ApiaryId"::int
WHERE u."Id" = m.new_id;

-- UniqueId and QrCodeBase64 are carried over unchanged: the QR image encodes
-- <frontend>/scan/<UniqueId>, so keeping the GUID keeps existing labels valid.
INSERT INTO "Beehives"
    ("Id", "Name", "Type", "Material", "DateCreated", "Notes", "LabelNumber",
     "UniqueId", "QrCodeBase64", "CreatedById", "ApiaryId", "CreatedAt", "UpdatedAt")
SELECT m.new_id,
       s."Name",
       s."Type"::int,
       s."Material"::int,
       s."DateCreated"::timestamptz,
       s."Notes",
       s."LabelNumber",
       s."UniqueId"::uuid,
       s."QrCodeBase64",
       um.new_id,
       am.new_id,
       s."CreatedAt"::timestamptz,
       s."UpdatedAt"::timestamptz
FROM staging.sel_beehives s
JOIN staging.id_map m       ON m.entity  = 'Beehives' AND m.old_id  = s."Id"::int
JOIN staging.id_map am      ON am.entity = 'Apiaries' AND am.old_id = s."ApiaryId"::int
LEFT JOIN staging.id_map um ON um.entity = 'Users'    AND um.old_id = s."CreatedById"::int;

INSERT INTO "Inspections"
    ("Id", "Date", "Temperature", "HoneyLevel", "BroodStatus", "Notes", "BeehiveId", "CreatedAt", "UpdatedAt")
SELECT m.new_id,
       s."Date"::timestamptz,
       s."Temperature"::double precision,
       s."HoneyLevel"::int,
       s."BroodStatus",
       s."Notes",
       bm.new_id,
       s."CreatedAt"::timestamptz,
       s."UpdatedAt"::timestamptz
FROM staging.sel_inspections s
JOIN staging.id_map m  ON m.entity  = 'Inspections' AND m.old_id  = s."Id"::int
JOIN staging.id_map bm ON bm.entity = 'Beehives'    AND bm.old_id = s."BeehiveId"::int;

INSERT INTO "Queens"
    ("Id", "Year", "MarkColor", "IsMarked", "IsClipped", "Origin", "Status",
     "IntroducedDate", "EndDate", "Notes", "BeehiveId", "CreatedAt", "UpdatedAt")
SELECT m.new_id,
       s."Year"::int,
       s."MarkColor"::int,
       s."IsMarked"::boolean,
       s."IsClipped"::boolean,
       s."Origin"::int,
       s."Status"::int,
       s."IntroducedDate"::timestamptz,
       s."EndDate"::timestamptz,
       s."Notes",
       bm.new_id,
       s."CreatedAt"::timestamptz,
       s."UpdatedAt"::timestamptz
FROM staging.sel_queens s
JOIN staging.id_map m  ON m.entity  = 'Queens'   AND m.old_id  = s."Id"::int
JOIN staging.id_map bm ON bm.entity = 'Beehives' AND bm.old_id = s."BeehiveId"::int;

-- Diets are still beehive-scoped here (pre-SPEC-12). If SPEC-12 has already been
-- deployed, this INSERT will fail on the missing "BeehiveId" column — see README.
INSERT INTO "Diets"
    ("Id", "Name", "StartDate", "Reason", "CustomReason", "DurationDays", "FrequencyDays",
     "FoodType", "CustomFoodType", "Status", "EarlyCompletionComment", "CreatedById",
     "BeehiveId", "CreatedAt", "UpdatedAt")
SELECT m.new_id,
       s."Name",
       s."StartDate"::timestamptz,
       s."Reason"::int,
       s."CustomReason",
       s."DurationDays"::int,
       s."FrequencyDays"::int,
       s."FoodType"::int,
       s."CustomFoodType",
       s."Status"::int,
       s."EarlyCompletionComment",
       um.new_id,
       bm.new_id,
       s."CreatedAt"::timestamptz,
       s."UpdatedAt"::timestamptz
FROM staging.sel_diets s
JOIN staging.id_map m       ON m.entity  = 'Diets'    AND m.old_id  = s."Id"::int
JOIN staging.id_map bm      ON bm.entity = 'Beehives' AND bm.old_id = s."BeehiveId"::int
LEFT JOIN staging.id_map um ON um.entity = 'Users'    AND um.old_id = s."CreatedById"::int;

INSERT INTO "FeedingEntries"
    ("Id", "ScheduledDate", "Status", "CompletionDate", "DietId", "CreatedAt", "UpdatedAt")
SELECT m.new_id,
       s."ScheduledDate"::timestamptz,
       s."Status"::int,
       s."CompletionDate"::timestamptz,
       dm.new_id,
       s."CreatedAt"::timestamptz,
       s."UpdatedAt"::timestamptz
FROM staging.sel_feedingentries s
JOIN staging.id_map m  ON m.entity  = 'FeedingEntries' AND m.old_id  = s."Id"::int
JOIN staging.id_map dm ON dm.entity = 'Diets'          AND dm.old_id = s."DietId"::int;

INSERT INTO "Treatments"
    ("Id", "ApiaryId", "Purpose", "ProductName", "ActiveSubstance", "Method", "DosePerHive",
     "StartDate", "EndDate", "WithdrawalDays", "BatchNumber", "Supplier", "Notes",
     "CreatedById", "CreatedAt", "UpdatedAt")
SELECT m.new_id,
       am.new_id,
       s."Purpose"::int,
       s."ProductName",
       s."ActiveSubstance"::int,
       s."Method"::int,
       s."DosePerHive",
       s."StartDate"::timestamptz,
       s."EndDate"::timestamptz,
       s."WithdrawalDays"::int,
       s."BatchNumber",
       s."Supplier",
       s."Notes",
       um.new_id,
       s."CreatedAt"::timestamptz,
       s."UpdatedAt"::timestamptz
FROM staging.sel_treatments s
JOIN staging.id_map m       ON m.entity  = 'Treatments' AND m.old_id  = s."Id"::int
JOIN staging.id_map am      ON am.entity = 'Apiaries'   AND am.old_id = s."ApiaryId"::int
LEFT JOIN staging.id_map um ON um.entity = 'Users'      AND um.old_id = s."CreatedById"::int;

INSERT INTO "TreatmentEntries"
    ("Id", "TreatmentId", "BeehiveId", "DoseNote", "CreatedAt", "UpdatedAt")
SELECT m.new_id,
       tm.new_id,
       bm.new_id,
       s."DoseNote",
       s."CreatedAt"::timestamptz,
       s."UpdatedAt"::timestamptz
FROM staging.sel_treatmententries s
JOIN staging.id_map m  ON m.entity  = 'TreatmentEntries' AND m.old_id  = s."Id"::int
JOIN staging.id_map tm ON tm.entity = 'Treatments'       AND tm.old_id = s."TreatmentId"::int
JOIN staging.id_map bm ON bm.entity = 'Beehives'         AND bm.old_id = s."BeehiveId"::int;

INSERT INTO "Todos"
    ("Id", "Title", "Notes", "DueDate", "Priority", "IsCompleted", "CompletedAt",
     "CreatedById", "AssignedToId", "ApiaryId", "BeehiveId", "CreatedAt", "UpdatedAt")
SELECT m.new_id,
       s."Title",
       s."Notes",
       s."DueDate"::timestamptz,
       s."Priority"::int,
       s."IsCompleted"::boolean,
       s."CompletedAt"::timestamptz,
       cm.new_id,
       aum.new_id,
       am.new_id,
       bm.new_id,
       s."CreatedAt"::timestamptz,
       s."UpdatedAt"::timestamptz
FROM staging.sel_todos s
JOIN staging.id_map m        ON m.entity   = 'Todos'    AND m.old_id   = s."Id"::int
LEFT JOIN staging.id_map cm  ON cm.entity  = 'Users'    AND cm.old_id  = s."CreatedById"::int
LEFT JOIN staging.id_map aum ON aum.entity = 'Users'    AND aum.old_id = s."AssignedToId"::int
LEFT JOIN staging.id_map am  ON am.entity  = 'Apiaries' AND am.old_id  = s."ApiaryId"::int
LEFT JOIN staging.id_map bm  ON bm.entity  = 'Beehives' AND bm.old_id  = s."BeehiveId"::int;

-- =============================================================================
-- POST-INSERT VERIFICATION — inside the transaction, so a mismatch rolls back.
-- =============================================================================

-- Exactly the expected number of mapped rows must now exist in each real table.
DO $$
DECLARE r record; actual int;
BEGIN
    FOR r IN SELECT entity, n FROM expected_counts LOOP
        EXECUTE format(
            'SELECT count(*) FROM %I t JOIN staging.id_map m ON m.entity = %L AND m.new_id = t."Id"',
            r.entity, r.entity) INTO actual;
        IF actual <> r.n THEN
            RAISE EXCEPTION 'ABORTED after insert: % holds % imported row(s), expected %.', r.entity, actual, r.n;
        END IF;
    END LOOP;
END $$;

-- Authorship must have survived the remap wherever the source had it.
DO $$
DECLARE bad int;
BEGIN
    SELECT count(*) INTO bad
    FROM staging.sel_beehives s
    JOIN staging.id_map m ON m.entity = 'Beehives' AND m.old_id = s."Id"::int
    JOIN "Beehives" b ON b."Id" = m.new_id
    WHERE s."CreatedById" IS NOT NULL AND b."CreatedById" IS NULL;
    IF bad > 0 THEN
        RAISE EXCEPTION 'ABORTED after insert: % beehive(s) lost their CreatedById in the remap.', bad;
    END IF;

    SELECT count(*) INTO bad
    FROM staging.sel_apiaries s
    JOIN staging.id_map m ON m.entity = 'Apiaries' AND m.old_id = s."Id"::int
    JOIN "Apiaries" a ON a."Id" = m.new_id
    WHERE s."CreatedById" IS NOT NULL AND a."CreatedById" IS NULL;
    IF bad > 0 THEN
        RAISE EXCEPTION 'ABORTED after insert: % apiary/apiaries lost their CreatedById in the remap.', bad;
    END IF;
END $$;

-- Every imported hive must sit under an imported apiary of an imported org.
DO $$
DECLARE bad int;
BEGIN
    SELECT count(*) INTO bad
    FROM staging.id_map m
    JOIN "Beehives" b  ON b."Id" = m.new_id AND m.entity = 'Beehives'
    JOIN "Apiaries" a  ON a."Id" = b."ApiaryId"
    WHERE NOT EXISTS (
        SELECT 1 FROM staging.id_map am
        WHERE am.entity = 'Apiaries' AND am.new_id = a."Id");
    IF bad > 0 THEN
        RAISE EXCEPTION 'ABORTED after insert: % imported beehive(s) attached to an apiary outside the import.', bad;
    END IF;
END $$;

\echo ''
\echo '=== Imported (old Id -> new Id) ==='
SELECT entity, count(*) AS rows, min(old_id) || '..' || max(old_id) AS old_ids,
       min(new_id) || '..' || max(new_id) AS new_ids
FROM staging.id_map GROUP BY entity ORDER BY entity;

COMMIT;

\echo ''
\echo 'Import committed.'
\echo 'Next: run 04_verify.sql, then have a SystemAdmin call'
\echo '  POST /api/beehives/regenerate-qr-codes'
\echo 'so the stored QR images point at the current domain instead of the old one.'
