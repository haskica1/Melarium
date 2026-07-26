-- =============================================================================
-- 01_load_csv.sql — Load the old-production CSV exports into a `staging` schema.
--
-- Nothing in this file touches a live Melarium table. Every staging column is
-- `text` so the load can never fail on a type mismatch: parsing and casting
-- happen in 02_import.sql, where a failure rolls back cleanly.
--
-- Column order in each CREATE TABLE must match the CSV header order exactly —
-- `\copy` maps by position, not by name.
--
-- Re-runnable: drops and recreates the whole staging schema.
-- =============================================================================

\set ON_ERROR_STOP on

DROP SCHEMA IF EXISTS staging CASCADE;
CREATE SCHEMA staging;

-- ─── Raw tables, one per CSV ─────────────────────────────────────────────────

CREATE TABLE staging.organizations (
    "Id" text, "Name" text, "Description" text, "CreatedById" text,
    "CreatedAt" text, "UpdatedAt" text, "Plan" text, "PlanNotes" text,
    "PlanValidUntil" text
);

CREATE TABLE staging.users (
    "Id" text, "FirstName" text, "LastName" text, "Email" text,
    "PasswordHash" text, "Role" text, "OrganizationId" text, "ApiaryId" text,
    "CreatedAt" text, "UpdatedAt" text
);

CREATE TABLE staging.apiaries (
    "Id" text, "Name" text, "Description" text, "Latitude" text, "Longitude" text,
    "OrganizationId" text, "CreatedById" text, "CreatedAt" text, "UpdatedAt" text,
    "CurrentPastureId" text, "HomeLatitude" text, "HomeLongitude" text
);

CREATE TABLE staging.beehives (
    "Id" text, "Name" text, "Type" text, "Material" text, "DateCreated" text,
    "Notes" text, "UniqueId" text, "QrCodeBase64" text, "CreatedById" text,
    "ApiaryId" text, "CreatedAt" text, "UpdatedAt" text, "LabelNumber" text
);

CREATE TABLE staging.inspections (
    "Id" text, "Date" text, "Temperature" text, "HoneyLevel" text,
    "BroodStatus" text, "Notes" text, "BeehiveId" text, "CreatedAt" text,
    "UpdatedAt" text
);

CREATE TABLE staging.queens (
    "Id" text, "Year" text, "MarkColor" text, "IsMarked" text, "IsClipped" text,
    "Origin" text, "Status" text, "IntroducedDate" text, "EndDate" text,
    "Notes" text, "BeehiveId" text, "CreatedAt" text, "UpdatedAt" text
);

CREATE TABLE staging.todos (
    "Id" text, "Title" text, "Notes" text, "DueDate" text, "Priority" text,
    "IsCompleted" text, "CompletedAt" text, "CreatedById" text,
    "AssignedToId" text, "ApiaryId" text, "BeehiveId" text, "CreatedAt" text,
    "UpdatedAt" text
);

CREATE TABLE staging.diets (
    "Id" text, "Name" text, "StartDate" text, "Reason" text, "CustomReason" text,
    "DurationDays" text, "FrequencyDays" text, "FoodType" text,
    "CustomFoodType" text, "Status" text, "EarlyCompletionComment" text,
    "CreatedById" text, "BeehiveId" text, "CreatedAt" text, "UpdatedAt" text
);

CREATE TABLE staging.feedingentries (
    "Id" text, "ScheduledDate" text, "Status" text, "CompletionDate" text,
    "DietId" text, "CreatedAt" text, "UpdatedAt" text
);

CREATE TABLE staging.treatments (
    "Id" text, "ApiaryId" text, "Purpose" text, "ProductName" text,
    "ActiveSubstance" text, "Method" text, "DosePerHive" text, "StartDate" text,
    "EndDate" text, "WithdrawalDays" text, "BatchNumber" text, "Supplier" text,
    "Notes" text, "CreatedById" text, "CreatedAt" text, "UpdatedAt" text
);

CREATE TABLE staging.treatmententries (
    "Id" text, "TreatmentId" text, "BeehiveId" text, "DoseNote" text,
    "CreatedAt" text, "UpdatedAt" text
);

CREATE TABLE staging.learningtopics (
    "Id" text, "Title" text, "Category" text, "Months" text, "Summary" text,
    "BodyMarkdown" text, "IsPublished" text, "PublishedAt" text,
    "CreatedAt" text, "UpdatedAt" text, "FileName" text, "FileUrl" text,
    "VideoUrl" text
);

-- ─── Load ────────────────────────────────────────────────────────────────────
-- `\copy` is a psql client-side command: these paths are resolved by whichever
-- machine runs psql. When running inside the postgres container (see README),
-- that means container paths.
--
-- FORMAT csv handles the multi-line quoted fields (apiary descriptions, the
-- learning-topic markdown bodies) correctly. An unquoted empty field becomes
-- NULL, a quoted "" stays an empty string — which matches how the old database
-- exported it.

\copy staging.organizations    FROM '/tmp/melarium-migration/Organizations_rows.csv'    WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
\copy staging.users            FROM '/tmp/melarium-migration/Users_rows.csv'            WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
\copy staging.apiaries         FROM '/tmp/melarium-migration/Apiaries_rows.csv'         WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
\copy staging.beehives         FROM '/tmp/melarium-migration/Beehives_rows.csv'         WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
\copy staging.inspections      FROM '/tmp/melarium-migration/Inspections_rows.csv'      WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
\copy staging.queens           FROM '/tmp/melarium-migration/Queens_rows.csv'           WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
\copy staging.todos            FROM '/tmp/melarium-migration/Todos_rows.csv'            WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
\copy staging.diets            FROM '/tmp/melarium-migration/Diets_rows.csv'            WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
\copy staging.feedingentries   FROM '/tmp/melarium-migration/FeedingEntries_rows.csv'   WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
\copy staging.treatments       FROM '/tmp/melarium-migration/Treatments_rows.csv'       WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
\copy staging.treatmententries FROM '/tmp/melarium-migration/TreatmentEntries_rows.csv' WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
\copy staging.learningtopics   FROM '/tmp/melarium-migration/LearningTopics_rows.csv'   WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')

-- ─── Selection: what actually gets imported ──────────────────────────────────
-- The whole filter hangs off this one list. Everything else is derived by
-- following foreign keys, so there is no second place to keep in sync.
--
--   6 = "AS Honey House"        (Asim Haskić, Semin Alihodžić)
--   7 = "Ćatić-Mekić family"    (Amel Ćatić)

CREATE TABLE staging.included_org (old_id int PRIMARY KEY);
INSERT INTO staging.included_org VALUES (6), (7);

CREATE VIEW staging.sel_organizations AS
SELECT * FROM staging.organizations
WHERE "Id"::int IN (SELECT old_id FROM staging.included_org);

CREATE VIEW staging.sel_users AS
SELECT * FROM staging.users
WHERE "OrganizationId"::int IN (SELECT old_id FROM staging.included_org);

CREATE VIEW staging.sel_apiaries AS
SELECT * FROM staging.apiaries
WHERE "OrganizationId"::int IN (SELECT old_id FROM staging.included_org);

CREATE VIEW staging.sel_beehives AS
SELECT * FROM staging.beehives
WHERE "ApiaryId"::int IN (SELECT "Id"::int FROM staging.sel_apiaries);

CREATE VIEW staging.sel_inspections AS
SELECT * FROM staging.inspections
WHERE "BeehiveId"::int IN (SELECT "Id"::int FROM staging.sel_beehives);

CREATE VIEW staging.sel_queens AS
SELECT * FROM staging.queens
WHERE "BeehiveId"::int IN (SELECT "Id"::int FROM staging.sel_beehives);

CREATE VIEW staging.sel_diets AS
SELECT * FROM staging.diets
WHERE "BeehiveId"::int IN (SELECT "Id"::int FROM staging.sel_beehives);

CREATE VIEW staging.sel_feedingentries AS
SELECT * FROM staging.feedingentries
WHERE "DietId"::int IN (SELECT "Id"::int FROM staging.sel_diets);

CREATE VIEW staging.sel_treatments AS
SELECT * FROM staging.treatments
WHERE "ApiaryId"::int IN (SELECT "Id"::int FROM staging.sel_apiaries);

CREATE VIEW staging.sel_treatmententries AS
SELECT * FROM staging.treatmententries
WHERE "TreatmentId"::int IN (SELECT "Id"::int FROM staging.sel_treatments);

-- A todo hangs off either an apiary or a hive (exactly one of the two).
CREATE VIEW staging.sel_todos AS
SELECT * FROM staging.todos
WHERE "ApiaryId"::int  IN (SELECT "Id"::int FROM staging.sel_apiaries)
   OR "BeehiveId"::int IN (SELECT "Id"::int FROM staging.sel_beehives);

-- ─── Report: loaded vs. selected ─────────────────────────────────────────────

\echo ''
\echo '=== Rows loaded from CSV / rows selected for import ==='
SELECT 'Organizations'    AS table_name, (SELECT count(*) FROM staging.organizations)    AS in_csv, (SELECT count(*) FROM staging.sel_organizations)    AS selected
UNION ALL SELECT 'Users',            (SELECT count(*) FROM staging.users),            (SELECT count(*) FROM staging.sel_users)
UNION ALL SELECT 'Apiaries',         (SELECT count(*) FROM staging.apiaries),         (SELECT count(*) FROM staging.sel_apiaries)
UNION ALL SELECT 'Beehives',         (SELECT count(*) FROM staging.beehives),         (SELECT count(*) FROM staging.sel_beehives)
UNION ALL SELECT 'Inspections',      (SELECT count(*) FROM staging.inspections),      (SELECT count(*) FROM staging.sel_inspections)
UNION ALL SELECT 'Queens',           (SELECT count(*) FROM staging.queens),           (SELECT count(*) FROM staging.sel_queens)
UNION ALL SELECT 'Diets',            (SELECT count(*) FROM staging.diets),            (SELECT count(*) FROM staging.sel_diets)
UNION ALL SELECT 'FeedingEntries',   (SELECT count(*) FROM staging.feedingentries),   (SELECT count(*) FROM staging.sel_feedingentries)
UNION ALL SELECT 'Todos',            (SELECT count(*) FROM staging.todos),            (SELECT count(*) FROM staging.sel_todos)
UNION ALL SELECT 'Treatments',       (SELECT count(*) FROM staging.treatments),       (SELECT count(*) FROM staging.sel_treatments)
UNION ALL SELECT 'TreatmentEntries', (SELECT count(*) FROM staging.treatmententries), (SELECT count(*) FROM staging.sel_treatmententries)
UNION ALL SELECT 'LearningTopics',   (SELECT count(*) FROM staging.learningtopics),   NULL
ORDER BY table_name;

\echo ''
\echo 'Staging loaded. Review the numbers above, then run 02_import.sql.'
