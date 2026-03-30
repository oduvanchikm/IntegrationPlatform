#!/bin/bash
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    -- Создаем схему
    CREATE SCHEMA IF NOT EXISTS target;
    
    -- Создаем таблицу для данных
    CREATE TABLE IF NOT EXISTS target.messages (
        id SERIAL PRIMARY KEY,
        message TEXT NOT NULL,
        created_at TIMESTAMP DEFAULT NOW()
    );
    
    -- Создаем индекс для быстрого поиска
    CREATE INDEX IF NOT EXISTS idx_target_created_at ON target.data(created_at);
    
    -- Даем права
    GRANT ALL PRIVILEGES ON DATABASE target_db TO admin;
    GRANT ALL ON SCHEMA target TO admin;
    GRANT ALL ON ALL TABLES IN SCHEMA target TO admin;
EOSQL

echo "Target database initialized"