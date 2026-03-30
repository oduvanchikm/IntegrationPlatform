#!/bin/bash

echo "Test Database to API integration"
echo "=========================================="

GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

# ==========================================
# 1. Подготовка базы данных
# ==========================================
echo -e "${BLUE}1. Подготовка базы данных...${NC}"

# Очищаем source базу
docker exec postgres-source psql -U admin -d source_db -c "TRUNCATE source.data;"

# Вставляем тестовые данные
docker exec postgres-source psql -U admin -d source_db -c "
INSERT INTO source.data (payload) VALUES 
    ('{\"message\": \"DB Record 1\", \"value\": 100}'),
    ('{\"message\": \"DB Record 2\", \"value\": 200}'),
    ('{\"message\": \"DB Record 3\", \"value\": 300}');"

echo -e "${GREEN}   ✓ Source данные добавлены (3 записи)${NC}"

# Очищаем target API
curl -s -X POST http://localhost:5101/api/target-data -H "Content-Type: application/json" -d '{"reset": true}' > /dev/null 2>&1

# ==========================================
# 2. Удаляем старые интерфейсы и связи
# ==========================================
echo -e "${BLUE}2. Очистка старых данных...${NC}"

for id in $(curl -s http://localhost:5003/api/Subscription/connections | jq -r '.[].id'); do
    curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$id 2>/dev/null
done

echo -e "${GREEN}   ✓ Старые данные очищены${NC}"

# ==========================================
# 3. Создание интерфейсов
# ==========================================
echo -e "${BLUE}3. Создание интерфейсов...${NC}"

# Source Database Interface
SOURCE_RESPONSE=$(curl -s -X POST http://localhost:5001/api/Publication/interfaces \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Source Database Interface",
    "productName": "SourceDBProduct",
    "interfaceType": 0,
    "description": "Source PostgreSQL database",
    "productType": 1,
    "host": "postgres-source",
    "port": "5432",
    "username": "admin",
    "password": "password",
    "databaseName": "source_db",
    "scheme": "source"
  }')

SOURCE_ID=$(echo $SOURCE_RESPONSE | jq -r '.interfaceId')
echo -e "${GREEN}✓ Source Database ID: $SOURCE_ID${NC}"

# Target API Interface
TARGET_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Interface \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Target API Interface",
    "productName": "APITargetProduct",
    "interfaceType": 2,
    "description": "Target API for testing",
    "host": "http://mock-api-target",
    "port": "8080",
    "endpoint": "/api/target-data"
  }')

TARGET_ID=$(echo $TARGET_RESPONSE | jq -r '.interfaceId')
echo -e "${GREEN}✓ Target API ID: $TARGET_ID${NC}"

# ==========================================
# 4. Тест 1: Единоразовое выполнение
# ==========================================
echo -e "${BLUE}4. Тест 1: Единоразовое выполнение (schedule: * * * * *)${NC}"

# Очищаем target API
curl -s -X POST http://localhost:5101/api/target-data -H "Content-Type: application/json" -d '{"reset": true}' > /dev/null 2>&1

CONNECT_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
  -H "Content-Type: application/json" \
  -d "{
    \"publicationInterfaceId\": $SOURCE_ID,
    \"subscriptionInterfaceId\": $TARGET_ID,
    \"integrationPattern\": 6,
    \"scheduleCron\": \"* * * * *\",
    \"maxRetryAttempts\": 3,
    \"retryDelaySeconds\": 30,
    \"executionTimeoutSeconds\": 300
  }")

echo "Connect response: $CONNECT_RESPONSE"
CONFIG_ID=$(echo $CONNECT_RESPONSE | jq -r '.orchestrationConfigId')
echo -e "${GREEN}✓ Config ID: $CONFIG_ID${NC}"

echo -e "${YELLOW}   Ожидание обработки Engine (10 сек)...${NC}"
sleep 10

TARGET_DATA=$(curl -s http://localhost:5101/api/target-data)
RECEIVED_COUNT=$(echo "$TARGET_DATA" | jq '.count // 0')
echo -e "${YELLOW}   Записей в API: $RECEIVED_COUNT${NC}"

if [ "$RECEIVED_COUNT" -eq 3 ]; then
    echo -e "${GREEN}   ✓ Единоразовое выполнение работает${NC}"
    echo "$TARGET_DATA" | jq '.receivedData'
else
    echo -e "${RED}   ✗ Ожидалось 3, получено $RECEIVED_COUNT${NC}"
fi

# Удаляем связь
curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$CONFIG_ID 2>/dev/null
sleep 2

# ==========================================
# 5. Тест 2: Расписание каждую минуту
# ==========================================
echo -e "${BLUE}5. Тест 2: Расписание каждую минуту (*/1 * * * *)${NC}"

# Очищаем target API
curl -s -X POST http://localhost:5101/api/target-data -H "Content-Type: application/json" -d '{"reset": true}' > /dev/null 2>&1

CONNECT_RESPONSE2=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
  -H "Content-Type: application/json" \
  -d "{
    \"publicationInterfaceId\": $SOURCE_ID,
    \"subscriptionInterfaceId\": $TARGET_ID,
    \"integrationPattern\": 6,
    \"scheduleCron\": \"*/1 * * * *\",
    \"maxRetryAttempts\": 3,
    \"retryDelaySeconds\": 30,
    \"executionTimeoutSeconds\": 300
  }")

echo "Connect response: $CONNECT_RESPONSE2"
CONFIG_ID2=$(echo $CONNECT_RESPONSE2 | jq -r '.orchestrationConfigId')
echo -e "${GREEN}✓ Config ID: $CONFIG_ID2${NC}"

echo -e "${YELLOW}   Ожидание 10 секунд для первой копии...${NC}"
sleep 10

TARGET_DATA=$(curl -s http://localhost:5101/api/target-data)
RECEIVED_COUNT=$(echo "$TARGET_DATA" | jq '.count // 0')
echo -e "${YELLOW}   Записей в API после первой копии: $RECEIVED_COUNT${NC}"

# Добавляем новые данные в source
echo -e "${YELLOW}   Добавляем новые данные в source...${NC}"
docker exec postgres-source psql -U admin -d source_db -c "
INSERT INTO source.data (payload) VALUES 
    ('{\"message\": \"New DB Record 4\", \"value\": 400}'),
    ('{\"message\": \"New DB Record 5\", \"value\": 500}');"

echo -e "${YELLOW}   Ожидание 70 секунд для срабатывания расписания...${NC}"
for i in {1..70}; do
    echo -ne "\r   Осталось $((70-i)) секунд..."
    sleep 1
done
echo ""

TARGET_DATA=$(curl -s http://localhost:5101/api/target-data)
RECEIVED_COUNT=$(echo "$TARGET_DATA" | jq '.count // 0')
echo -e "${YELLOW}   Записей в API после добавления: $RECEIVED_COUNT${NC}"

if [ "$RECEIVED_COUNT" -eq 5 ]; then
    echo -e "${GREEN}   ✓ Расписание работает! Все 5 записей переданы${NC}"
else
    echo -e "${RED}   ✗ Ожидалось 5, получено $RECEIVED_COUNT${NC}"
fi

# ==========================================
# 6. Просмотр логов Engine
# ==========================================
echo -e "${BLUE}6. Последние логи Engine:${NC}"
docker logs --tail 50 integration-engine | grep -E "DATABASE TO API|Running scheduled copy|Successfully copied"

echo -e "\n${GREEN}✅ Тест Database → API завершен!${NC}"