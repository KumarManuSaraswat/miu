-- Run once as the owner in a DEDICATED catalog database, before enabling login.
-- No passwords belong in migrations. This file creates an initially NOLOGIN role.
BEGIN;

CREATE SCHEMA miu_catalog;
REVOKE ALL ON SCHEMA miu_catalog FROM PUBLIC;
CREATE TABLE miu_catalog.presets (
    id varchar(64) PRIMARY KEY CHECK (id ~ '^[a-z0-9][a-z0-9-]{0,63}$'),
    name varchar(80) NOT NULL CHECK (length(name) > 0 AND name = btrim(name) AND name !~ '[[:cntrl:]<>]'),
    description varchar(240) NOT NULL CHECK (length(description) > 0 AND description = btrim(description) AND description !~ '[[:cntrl:]<>]'),
    mood varchar(32) NOT NULL CHECK (mood IN (
        'idle', 'happy', 'curious', 'excited', 'love', 'sleepy', 'angry', 'confused',
        'thinking', 'listening', 'speaking', 'wink', 'surprised',
        'content', 'kiss', 'crying', 'neutral', 'playful', 'wailing', 'mischievous',
        'smug_kiss', 'sassy', 'wide_eyed', 'surprised_kiss', 'shocked', 'joyful', 'sad',
        'annoyed', 'scrunched', 'dizzy', 'blushing', 'cheerful', 'drooling',
        'delighted', 'skeptical', 'furious', 'amazed'
    )),
    speech_rate smallint NOT NULL CHECK (speech_rate BETWEEN -5 AND 5),
    reduced_motion boolean NOT NULL,
    sort_order smallint NOT NULL DEFAULT 0 CHECK (sort_order >= 0),
    published boolean NOT NULL DEFAULT true
);
CREATE INDEX presets_published_order ON miu_catalog.presets (sort_order, id) WHERE published = true;
REVOKE ALL ON miu_catalog.presets FROM PUBLIC;
ALTER DEFAULT PRIVILEGES IN SCHEMA miu_catalog REVOKE ALL ON TABLES FROM PUBLIC;

-- SQL-created roles do not receive Neon's console-created superuser membership.
-- Fail if the name already exists; never silently reuse a more privileged role.
CREATE ROLE miu_catalog_reader NOLOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
GRANT USAGE ON SCHEMA miu_catalog TO miu_catalog_reader;
GRANT SELECT ON miu_catalog.presets TO miu_catalog_reader;
ALTER ROLE miu_catalog_reader SET default_transaction_read_only = on;
ALTER ROLE miu_catalog_reader SET statement_timeout = '4s';
ALTER ROLE miu_catalog_reader SET lock_timeout = '1s';
ALTER ROLE miu_catalog_reader SET search_path = pg_catalog;

-- In this dedicated database, remove PUBLIC's inherited object-creation rights.
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
DO $$ BEGIN
    EXECUTE format('REVOKE CREATE, TEMPORARY ON DATABASE %I FROM PUBLIC', current_database());
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO miu_catalog_reader', current_database());
END $$;
COMMIT;
