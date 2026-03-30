#!/bin/bash

echo "Test Kafka to Database integration"
echo "=========================================="

GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

TIMESTAMP=$(date +%s)
SOURCE_TOPIC="source-topic-kafka-db-${TIMESTAMP}"

# ==========================================
# 1. Подготовка Kafka и базы данных
# ==========================================
echo -e "${BLUE}1. Подготовка Kafka и базы данных...${NC}"

# Создаем топик в Kafka2
docker exec kafka2 kafka-topics --create \
  --topic "$SOURCE_TOPIC" \
  --bootstrap-server kafka2:9092 \
  --partitions 1 \
  --replication-factor 1 2>/dev/null || echo "Topic exists"

# Очищаем target базу
docker exec postgres-target psql -U admin -d target_db -c "TRUNCATE target.data;"

echo -e "${GREEN}   ✓ Kafka топик создан: $SOURCE_TOPIC${NC}"
echo -e "${GREEN}   ✓ Target база очищена${NC}"

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

# Source Kafka Interface
SOURCE_RESPONSE=$(curl -s -X POST http://localhost:5001/api/Publication/interfaces \
  -H "Content-Type: application/json" \
  -d "{
    \"name\": \"Source Kafka Interface\",
    \"productName\": \"KafkaSourceProduct\",
    \"interfaceType\": 1,
    \"description\": \"Source Kafka for testing\",
    \"productType\": 1,
    \"bootstrapServers\": \"kafka2:9092\",
    \"topicName\": \"$SOURCE_TOPIC\",
    \"username\": \"\",
    \"password\": \"\"
  }")

SOURCE_ID=$(echo $SOURCE_RESPONSE | jq -r '.interfaceId')
echo -e "${GREEN}✓ Source Kafka ID: $SOURCE_ID${NC}"

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
# 4. Создание интеграции (стриминг, без расписания)
# ==========================================
echo -e "${BLUE}4. Создание интеграции Kafka → Database...${NC}"

CONNECT_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
  -H "Content-Type: application/json" \
  -d "{
    \"publicationInterfaceId\": $SOURCE_ID,
    \"subscriptionInterfaceId\": $TARGET_ID,
    \"integrationPattern\": 5,
    \"scheduleCron\": \"* * * * *\",
    \"maxRetryAttempts\": 3,
    \"retryDelaySeconds\": 30,
    \"executionTimeoutSeconds\": 300
  }")

echo "Connect response: $CONNECT_RESPONSE"
CONFIG_ID=$(echo $CONNECT_RESPONSE | jq -r '.orchestrationConfigId')
echo -e "${GREEN}✓ Config ID: $CONFIG_ID${NC}"

echo -e "${YELLOW}   Ожидание инициализации стриминга (10 сек)...${NC}"
sleep 10

# ==========================================
# 5. Тест 1: Отправка сообщений в Kafka
# ==========================================
echo -e "${BLUE}5. Тест 1: Отправка сообщений в Kafka...${NC}"

echo -e "${YELLOW}   Отправка 3 сообщений...${NC}"
for i in {1..3}; do
    echo "Kafka to Database message $i" | docker exec -i kafka2 kafka-console-producer --broker-list kafka2:9092 --topic "$SOURCE_TOPIC"
    echo -e "${YELLOW}   Сообщение $i отправлено${NC}"
    sleep 1
done

echo -e "${YELLOW}   Ожидание обработки Engine (5 сек)...${NC}"
sleep 5

TARGET_COUNT=$(docker exec postgres-target psql -U admin -d target_db -t -c "SELECT COUNT(*) FROM target.data;" | tr -d ' ')
echo -e "${YELLOW}   Записей в target базе: $TARGET_COUNT${NC}"

if [ "$TARGET_COUNT" -eq 3 ]; then
    echo -e "${GREEN}   ✓ Все 3 сообщения сохранены в базу${NC}"
    docker exec postgres-target psql -U admin -d target_db -c "SELECT id, payload->>'message' as message, created_at FROM target.data;" 2>/dev/null
else
    echo -e "${RED}   ✗ Ожидалось 3, получено $TARGET_COUNT${NC}"
fi

# ==========================================
# 6. Тест 2: Непрерывная отправка
# ==========================================
echo -e "${BLUE}6. Тест 2: Непрерывная отправка...${NC}"

echo -e "${YELLOW}   Отправка еще 2 сообщений...${NC}"
for i in {4..5}; do
    echo "Kafka to Database message $i" | docker exec -i kafka2 kafka-console-producer --broker-list kafka2:9092 --topic "$SOURCE_TOPIC"
    echo -e "${YELLOW}   Сообщение $i отправлено${NC}"
    sleep 1
done

echo -e "${YELLOW}   Ожидание обработки Engine (3 сек)...${NC}"
sleep 3

TARGET_COUNT=$(docker exec postgres-target psql -U admin -d target_db -t -c "SELECT COUNT(*) FROM target.data;" | tr -d ' ')
echo -e "${YELLOW}   Записей в target базе: $TARGET_COUNT${NC}"

if [ "$TARGET_COUNT" -eq 5 ]; then
    echo -e "${GREEN}   ✓ Все 5 сообщений сохранены в базу${NC}"
else
    echo -e "${RED}   ✗ Ожидалось 5, получено $TARGET_COUNT${NC}"
fi

# ==========================================
# 7. Просмотр логов Engine
# ==========================================
echo -e "${BLUE}7. Последние логи Engine:${NC}"
docker logs --tail 50 integration-engine | grep -E "KAFKA TO DATABASE|Received message|Message written to database"

echo -e "\n${GREEN}✅ Тест Kafka → Database завершен!${NC}"