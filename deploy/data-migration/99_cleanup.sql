-- =============================================================================
-- 99_cleanup.sql — Drop the staging schema.
--
-- Run only once the import has been verified AND staging.id_map has been saved
-- somewhere durable. id_map is the only record of which old Id became which new
-- Id; without it, tracing an imported row back to the old database is guesswork.
--
-- Export it first:
--   \copy (SELECT * FROM staging.id_map ORDER BY entity, old_id) TO '/tmp/melarium-migration/id_map.csv' WITH (FORMAT csv, HEADER true)
--
-- Dropping this schema does not touch any imported data.
-- =============================================================================

\set ON_ERROR_STOP on

\echo ''
\echo 'staging.id_map contents about to be dropped:'
SELECT entity, count(*) AS rows FROM staging.id_map GROUP BY entity ORDER BY entity;

DROP SCHEMA staging CASCADE;

\echo ''
\echo 'Staging schema dropped.'
