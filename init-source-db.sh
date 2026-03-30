#!/bin/bash
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    -- Создаем схему
    CREATE SCHEMA IF NOT EXISTS source;
    
    -- Создаем таблицу для данных
    CREATE TABLE IF NOT EXISTS source.data (
        id SERIAL PRIMARY KEY,
        payload JSONB NOT NULL,
        created_at TIMESTAMP DEFAULT NOW()
    );
    
    -- Вставляем тестовые данные
    INSERT INTO source.data (payload) VALUES 
        ('{"message": "Test record 1", "value": 100}'),
        ('{"message": "Test record 2", "value": 200}'),
        ('{"message": "Test record 3", "value": 300}');
    
    -- Создаем пользователя для репликации (если нужно)
    CREATE USER replicator WITH REPLICATION ENCRYPTED PASSWORD 'replicator_password';
    
    -- Даем права
    GRANT ALL PRIVILEGES ON DATABASE source_db TO admin;
    GRANT ALL ON SCHEMA source TO admin;
    GRANT ALL ON ALL TABLES IN SCHEMA source TO admin;
EOSQL

echo "Source database initialized"