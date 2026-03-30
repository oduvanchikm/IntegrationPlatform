#!/bin/bash

echo "Test API to Database integration"
echo "=========================================="

GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

# ==========================================
# 1. Подготовка базы данных
# ==========================================
#echo -e "${BLUE}1. Подготовка базы данных...${NC}"

# Очищаем target базу
#docker exec postgres-target psql -U admin -d target_db -c "TRUNCATE target.data;"

#echo -e "${GREEN}   ✓ Target база очищена${NC}"

# ==========================================
# 2. Удаляем старые интерфейсы и связи
# ==========================================
#echo -e "${BLUE}2. Очистка старых данных...${NC}"
#
#for id in $(curl -s http://localhost:5003/api/Subscription/connections | jq -r '.[].id'); do
#    curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$id 2>/dev/null
#done
#
#echo -e "${GREEN}   ✓ Старые данные очищены${NC}"

# ==========================================
# 3. Создание интерфейсов
# ==========================================
echo -e "${BLUE}3. Создание интерфейсов...${NC}"

# Source API Interface
SOURCE_RESPONSE=$(curl -s -X POST http://localhost:5001/api/Publication/interfaces \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Source API Interface",
    "productName": "APISourceProduct",
    "interfaceType": 2,
    "description": "Source API for testing",
    "productType": 1,
    "host": "http://mock-api-source",
    "port": "8080",
    "endpoint": "/api/source-data",
    "username": "",
    "password": "",
    "token": ""
  }')

SOURCE_ID=$(echo $SOURCE_RESPONSE | jq -r '.interfaceId')
echo -e "${GREEN}✓ Source API ID: $SOURCE_ID${NC}"

# Target Database Interface
TARGET_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Interface \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Target Database Interface",
    "productName": "TargetDBProduct",
    "interfaceType": 0,
    "description": "Target PostgreSQL database",
    "host": "postgres-target",
    "port": "5432",
    "username": "admin",
    "password": "password",
    "databaseName": "target_db",
    "scheme": "target"
  }')

TARGET_ID=$(echo $TARGET_RESPONSE | jq -r '.interfaceId')
echo -e "${GREEN}✓ Target Database ID: $TARGET_ID${NC}"

# ==========================================
# 4. Тест 1: Единоразовое выполнение
# ==========================================
echo -e "${BLUE}4. Тест 1: Единоразовое выполнение (schedule: * * * * *)${NC}"

# Очищаем target базу
docker exec postgres-target psql -U admin -d target_db -c "TRUNCATE target.data;"

CONNECT_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
  -H "Content-Type: application/json" \
  -d "{
    \"publicationInterfaceId\": $SOURCE_ID,
    \"subscriptionInterfaceId\": $TARGET_ID,
    \"integrationPattern\": 3,
    \"scheduleCron\": \"* * * * *\",
    \"maxRetryAttempts\": 3,
    \"retryDelaySeconds\": 30,
    \"executionTimeoutSeconds\": 300
  }")

echo "Connect response: $CONNECT_RESPONSE"
CONFIG_ID=$(echo $CONNECT_RESPONSE | jq -r '.orchestrationConfigId')
echo -e "${GREEN}✓ Config ID: $CONFIG_ID${NC}"

echo -e "${YELLOW}   Отправка тестовых данных в source API...${NC}"
curl -s -X POST http://localhost:5100/api/source-data \
  -H "Content-Type: application/json" \
  -d '{
    "message": "API to Database test",
    "value": 100,
    "timestamp": "'$(date -Iseconds)'"
  }' | jq .

echo -e "${YELLOW}   Ожидание обработки Engine (5 сек)...${NC}"
sleep 5

TARGET_COUNT=$(docker exec postgres-target psql -U admin -d target_db -t -c "SELECT COUNT(*) FROM target.data;" | tr -d ' ')
echo -e "${YELLOW}   Записей в target базе: $TARGET_COUNT${NC}"

if [ "$TARGET_COUNT" -eq 1 ]; then
    echo -e "${GREEN}   ✓ Единоразовое выполнение работает${NC}"
    docker exec postgres-target psql -U admin -d target_db -c "SELECT id, payload->>'message' as message, created_at FROM target.data;" 2>/dev/null
else
    echo -e "${RED}   ✗ Ожидалось 1, получено $TARGET_COUNT${NC}"
fi

# Удаляем связь
curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$CONFIG_ID 2>/dev/null
sleep 2

# ==========================================
# 5. Тест 2: Расписание каждую минуту
# ==========================================
echo -e "${BLUE}5. Тест 2: Расписание каждую минуту (*/1 * * * *)${NC}"

# Очищаем target базу
docker exec postgres-target psql -U admin -d target_db -c "TRUNCATE target.data;"

CONNECT_RESPONSE2=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
  -H "Content-Type: application/json" \
  -d "{
    \"publicationInterfaceId\": $SOURCE_ID,
    \"subscriptionInterfaceId\": $TARGET_ID,
    \"integrationPattern\": 3,
    \"scheduleCron\": \"*/1 * * * *\",
    \"maxRetryAttempts\": 3,
    \"retryDelaySeconds\": 30,
    \"executionTimeoutSeconds\": 300
  }")

echo "Connect response: $CONNECT_RESPONSE2"
CONFIG_ID2=$(echo $CONNECT_RESPONSE2 | jq -r '.orchestrationConfigId')
echo -e "${GREEN}✓ Config ID: $CONFIG_ID2${NC}"

echo -e "${YELLOW}   Отправка первого сообщения...${NC}"
curl -s -X POST http://localhost:5100/api/source-data \
  -H "Content-Type: application/json" \
  -d '{"message": "First message", "value": 100}' > /dev/null

echo -e "${YELLOW}   Ожидание 10 секунд для первой копии...${NC}"
sleep 10

TARGET_COUNT=$(docker exec postgres-target psql -U admin -d target_db -t -c "SELECT COUNT(*) FROM target.data;" | tr -d ' ')
echo -e "${YELLOW}   Записей в target после первой копии: $TARGET_COUNT${NC}"

# Отправляем новые данные
echo -e "${YELLOW}   Отправка новых сообщений...${NC}"
for i in {2..4}; do
    curl -s -X POST http://localhost:5100/api/source-data \
      -H "Content-Type: application/json" \
      -d "{\"message\": \"Message $i\", \"value\": $((i*100))}" > /dev/null
    echo -e "${YELLOW}   Сообщение $i отправлено${NC}"
    sleep 1
done

echo -e "${YELLOW}   Ожидание 70 секунд для срабатывания расписания...${NC}"
for i in {1..70}; do
    echo -ne "\r   Осталось $((70-i)) секунд..."
    sleep 1
done
echo ""

TARGET_COUNT=$(docker exec postgres-target psql -U admin -d target_db -t -c "SELECT COUNT(*) FROM target.data;" | tr -d ' ')
echo -e "${YELLOW}   Записей в target после добавления: $TARGET_COUNT${NC}"

if [ "$TARGET_COUNT" -eq 4 ]; then
    echo -e "${GREEN}   ✓ Расписание работает! Все 4 сообщения сохранены${NC}"
else
    echo -e "${RED}   ✗ Ожидалось 4, получено $TARGET_COUNT${NC}"
fi

# ==========================================
# 6. Просмотр логов Engine
# ==========================================
echo -e "${BLUE}6. Последние логи Engine:${NC}"
docker logs --tail 50 integration-engine | grep -E "API TO DATABASE|Data received from API|Successfully saved"

echo -e "\n${GREEN}✅ Тест API → Database завершен!${NC}"