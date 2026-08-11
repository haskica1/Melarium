-- =============================================================================
-- 01_backfill.sql — Copy AdvisorConversations/AdvisorMessages into the unified
-- AiAssistantSessions/AiAssistantTurns shape (SPEC-18).
--
-- Runs as ONE transaction. Every migrated turn gets ZERO AiAssistantAction
-- rows — under SPEC-18 that is exactly correct, not a gap: the advisor never
-- proposed an action, so "no actions" is the honest fact for a migrated
-- question-and-answer turn. RawModelJson/CandidatesJson/Transcript are left
-- NULL for the same reason (the advisor never recorded any of the three).
--
-- ID POLICY — same spirit as the July old-database import: no old Id is
-- reused. Every session/turn gets a fresh Id from AiAssistantSessions'/
-- AiAssistantTurns' own identity sequence (via a plain INSERT — Postgres
-- assigns it), and the old->new mapping is captured row-by-row in an
-- explicit PL/pgSQL loop, in staging.advisor_merge_id_map. A loop, not a
-- RETURNING-plus-row_number() join, because Postgres does not guarantee that
-- INSERT ... SELECT ... RETURNING preserves the SELECT's row order — a loop
-- pairs each old Id with the new Id it just created, so there is nothing to
-- assume.
--
-- Role mapping is written explicitly (CASE ... WHEN), not assumed identical
-- to AdvisorRole, even though the two enums carry the same numbers today —
-- AiTurnRole's own doc comment says the split from AdvisorRole is deliberate,
-- so a future divergence between them must not become a silent bug here.
--
-- Prerequisite: the AiAssistantSession.BeehiveId migration must already be
-- applied (adds the column this script writes into) — it shipped with
-- SPEC-18's backend changes, ahead of this script.
--
-- NOT idempotent by design, like the rest of this folder: running it twice
-- would duplicate every row. The pre-flight check below refuses a second run.
-- =============================================================================

\set ON_ERROR_STOP on

BEGIN;

-- ─── Pre-flight: refuse a second run ─────────────────────────────────────────

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'staging' AND table_name = 'advisor_merge_id_map'
    ) THEN
        RAISE EXCEPTION
            'staging.advisor_merge_id_map already exists — this migration has already run (or a prior attempt was not cleaned up). Refusing to run again to avoid duplicating rows.';
    END IF;
END $$;

CREATE SCHEMA IF NOT EXISTS staging;

CREATE TABLE staging.advisor_merge_id_map (
    entity text NOT NULL,
    old_id int  NOT NULL,
    new_id int  NOT NULL,
    PRIMARY KEY (entity, old_id),
    UNIQUE (entity, new_id)
);
COMMENT ON TABLE staging.advisor_merge_id_map IS
    'AdvisorConversations/AdvisorMessages Id -> AiAssistantSessions/AiAssistantTurns Id (SPEC-18). Export before dropping the old tables in the later, separate cleanup deploy.';

-- ─── Migrate, one conversation at a time ─────────────────────────────────────

DO $$
DECLARE
    conv          RECORD;
    msg           RECORD;
    new_session_id int;
    new_turn_id    int;
BEGIN
    FOR conv IN SELECT * FROM "AdvisorConversations" ORDER BY "Id" LOOP
        INSERT INTO "AiAssistantSessions" ("UserId", "BeehiveId", "Title", "CreatedAt", "UpdatedAt")
        VALUES (conv."UserId", conv."BeehiveId", conv."Title", conv."CreatedAt", conv."UpdatedAt")
        RETURNING "Id" INTO new_session_id;

        INSERT INTO staging.advisor_merge_id_map (entity, old_id, new_id)
        VALUES ('AdvisorConversation', conv."Id", new_session_id);

        FOR msg IN SELECT * FROM "AdvisorMessages" WHERE "ConversationId" = conv."Id" ORDER BY "Id" LOOP
            INSERT INTO "AiAssistantTurns" ("SessionId", "Role", "Content", "CreatedAt", "UpdatedAt")
            VALUES (
                new_session_id,
                CASE msg."Role" WHEN 1 THEN 1 WHEN 2 THEN 2 END, -- User=1, Assistant=2 in both enums
                msg."Content", msg."CreatedAt", msg."UpdatedAt"
            )
            RETURNING "Id" INTO new_turn_id;

            INSERT INTO staging.advisor_merge_id_map (entity, old_id, new_id)
            VALUES ('AdvisorMessage', msg."Id", new_turn_id);
        END LOOP;
    END LOOP;
END $$;

-- ─── Sanity check before commit ──────────────────────────────────────────────

DO $$
DECLARE
    old_sessions int; new_sessions int;
    old_turns    int; new_turns    int;
BEGIN
    SELECT count(*) INTO old_sessions FROM "AdvisorConversations";
    SELECT count(*) INTO new_sessions FROM staging.advisor_merge_id_map WHERE entity = 'AdvisorConversation';
    SELECT count(*) INTO old_turns    FROM "AdvisorMessages";
    SELECT count(*) INTO new_turns    FROM staging.advisor_merge_id_map WHERE entity = 'AdvisorMessage';

    IF old_sessions <> new_sessions THEN
        RAISE EXCEPTION 'Session count mismatch: % AdvisorConversations vs % migrated', old_sessions, new_sessions;
    END IF;
    IF old_turns <> new_turns THEN
        RAISE EXCEPTION 'Turn count mismatch: % AdvisorMessages vs % migrated', old_turns, new_turns;
    END IF;

    RAISE NOTICE 'Migrated % session(s) and % turn(s).', new_sessions, new_turns;
END $$;

COMMIT;
