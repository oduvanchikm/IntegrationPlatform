#!/bin/bash
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "integration_platform" <<-EOSQL
    CREATE SCHEMA IF NOT EXISTS publication;
    CREATE SCHEMA IF NOT EXISTS subscription;
    CREATE SCHEMA IF NOT EXISTS discovery;
    
    GRANT ALL PRIVILEGES ON SCHEMA publication TO admin;
    GRANT ALL PRIVILEGES ON SCHEMA subscription TO admin;
    GRANT ALL PRIVILEGES ON SCHEMA discovery TO admin;
EOSQL