-- =============================================================================
-- 03_learning_topics.sql — OPTIONAL. Import the learning topics (SPEC-06).
--
-- Separate from 02_import.sql on purpose: learning topics are platform-wide
-- content authored by the SystemAdmin, not organization data. They carry no
-- organization FK, so the "only these two organizations" filter says nothing
-- about them — importing them is a separate decision. Skip this file if the
-- articles have already been re-authored on the new production.
--
-- Idempotent: a topic whose Title already exists is skipped, so re-running
-- never duplicates an article. LearningTopicReads (per-user read tracking) is
-- not part of the export, so read state starts empty.
--
-- Requires 01_load_csv.sql to have run.
-- =============================================================================

\set ON_ERROR_STOP on

BEGIN;

CREATE TABLE IF NOT EXISTS staging.id_map (
    entity text NOT NULL,
    old_id int  NOT NULL,
    new_id int  NOT NULL,
    PRIMARY KEY (entity, old_id),
    UNIQUE (entity, new_id)
);

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
    RETURN setval(seq, GREATEST(maxid, nextval(seq)));
END $$;

-- Only topics that are not already present, matched on Title.
CREATE OR REPLACE VIEW staging.sel_learningtopics AS
SELECT s.* FROM staging.learningtopics s
WHERE NOT EXISTS (SELECT 1 FROM "LearningTopics" t WHERE t."Title" = s."Title");

\echo ''
\echo '=== Learning topics: in CSV / already present / to import ==='
SELECT (SELECT count(*) FROM staging.learningtopics)                                       AS in_csv,
       (SELECT count(*) FROM staging.learningtopics) -
       (SELECT count(*) FROM staging.sel_learningtopics)                                   AS already_present,
       (SELECT count(*) FROM staging.sel_learningtopics)                                   AS to_import;

SELECT staging.sync_seq('LearningTopics');

INSERT INTO staging.id_map (entity, old_id, new_id)
SELECT 'LearningTopics', t.old_id, nextval(staging.seq_for('LearningTopics'))
FROM (SELECT "Id"::int AS old_id FROM staging.sel_learningtopics ORDER BY 1) t
ON CONFLICT (entity, old_id) DO NOTHING;

-- Months arrives as a JSON-style list ("[7,8]") but the column is a Postgres
-- integer[], so the brackets are translated to braces. NULL/empty = evergreen.
INSERT INTO "LearningTopics"
    ("Id", "Title", "Category", "Months", "Summary", "BodyMarkdown",
     "VideoUrl", "FileUrl", "FileName", "IsPublished", "PublishedAt",
     "CreatedAt", "UpdatedAt")
SELECT m.new_id,
       s."Title",
       s."Category"::int,
       NULLIF(translate(s."Months", '[]', '{}'), '')::int[],
       s."Summary",
       s."BodyMarkdown",
       s."VideoUrl",
       s."FileUrl",
       s."FileName",
       s."IsPublished"::boolean,
       s."PublishedAt"::timestamptz,
       s."CreatedAt"::timestamptz,
       s."UpdatedAt"::timestamptz
FROM staging.sel_learningtopics s
JOIN staging.id_map m ON m.entity = 'LearningTopics' AND m.old_id = s."Id"::int;

-- PublishedAt is the once-per-topic notification guard. Every published topic in
-- the export already has it set, so importing them fires no notification storm —
-- verify that assumption rather than trusting it.
DO $$
DECLARE bad int;
BEGIN
    SELECT count(*) INTO bad
    FROM "LearningTopics" t
    JOIN staging.id_map m ON m.entity = 'LearningTopics' AND m.new_id = t."Id"
    WHERE t."IsPublished" AND t."PublishedAt" IS NULL;
    IF bad > 0 THEN
        RAISE EXCEPTION
            'ABORTED: % imported topic(s) are published but have no PublishedAt — they would be treated as newly published.', bad;
    END IF;
END $$;

\echo ''
\echo '=== Learning topics now in the database ==='
SELECT "Id", "Category", "IsPublished", "Months", left("Title", 60) AS title
FROM "LearningTopics" ORDER BY "Id";

COMMIT;

\echo ''
\echo 'Learning topics committed.'
