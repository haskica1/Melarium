-- =============================================================================
-- 02_verify.sql — Read-only checks after 01_backfill.sql. Every section should
-- report zero mismatches / zero rows, as noted per section.
-- =============================================================================

-- 1. Row counts: every AdvisorConversation/AdvisorMessage has exactly one
--    migrated counterpart, and vice versa. Expect both rows' counts to match.
SELECT
    (SELECT count(*) FROM "AdvisorConversations")                                            AS advisor_conversations,
    (SELECT count(*) FROM staging.advisor_merge_id_map WHERE entity = 'AdvisorConversation')  AS migrated_sessions,
    (SELECT count(*) FROM "AdvisorMessages")                                                  AS advisor_messages,
    (SELECT count(*) FROM staging.advisor_merge_id_map WHERE entity = 'AdvisorMessage')       AS migrated_turns;

-- 2. Content fidelity: every migrated session's Title/UserId/BeehiveId and every
--    migrated turn's Content/Role match their source row exactly. Expect ZERO rows.
SELECT s_map.old_id AS old_conversation_id, s_map.new_id AS new_session_id
FROM staging.advisor_merge_id_map s_map
JOIN "AdvisorConversations" c ON c."Id" = s_map.old_id
JOIN "AiAssistantSessions" s ON s."Id" = s_map.new_id
WHERE s_map.entity = 'AdvisorConversation'
  AND (s."UserId" IS DISTINCT FROM c."UserId"
   OR  s."BeehiveId" IS DISTINCT FROM c."BeehiveId"
   OR  s."Title" IS DISTINCT FROM c."Title");

SELECT m_map.old_id AS old_message_id, m_map.new_id AS new_turn_id
FROM staging.advisor_merge_id_map m_map
JOIN "AdvisorMessages" m ON m."Id" = m_map.old_id
JOIN "AiAssistantTurns" t ON t."Id" = m_map.new_id
WHERE m_map.entity = 'AdvisorMessage'
  AND (t."Content" IS DISTINCT FROM m."Content"
   OR  t."Role" IS DISTINCT FROM m."Role");

-- 3. Every migrated turn belongs to a migrated session (not some unrelated,
--    pre-existing AiAssistantSession) and sits in the right one. Expect ZERO rows.
SELECT m_map.old_id AS old_message_id
FROM staging.advisor_merge_id_map m_map
JOIN "AdvisorMessages" m   ON m."Id" = m_map.old_id
JOIN "AiAssistantTurns" t  ON t."Id" = m_map.new_id
JOIN staging.advisor_merge_id_map c_map
     ON c_map.entity = 'AdvisorConversation' AND c_map.old_id = m."ConversationId"
WHERE m_map.entity = 'AdvisorMessage'
  AND t."SessionId" <> c_map.new_id;

-- 4. Migrated turns must carry zero proposed actions — a question is not a
--    command, and the migration must not have accidentally created any.
--    Expect ZERO rows.
SELECT t."Id" AS turn_id, count(a."Id") AS action_count
FROM "AiAssistantTurns" t
JOIN staging.advisor_merge_id_map m_map ON m_map.entity = 'AdvisorMessage' AND m_map.new_id = t."Id"
JOIN "AiAssistantActions" a ON a."TurnId" = t."Id"
GROUP BY t."Id";

-- 5. Spot-check: five migrated sessions, newest first, to eyeball in the app
--    afterwards (log in as the listed user, open /assistant, find the title).
SELECT s."Id" AS session_id, s."UserId", s."Title", s."CreatedAt"
FROM "AiAssistantSessions" s
JOIN staging.advisor_merge_id_map s_map ON s_map.entity = 'AdvisorConversation' AND s_map.new_id = s."Id"
ORDER BY s."CreatedAt" DESC
LIMIT 5;
