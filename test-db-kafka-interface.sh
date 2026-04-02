#!/bin/bash

echo "╔══════════════════════════════════════════════════════════════════════════════╗"
echo "║                    Database → Kafka Integration Test                         ║"
echo "╚══════════════════════════════════════════════════════════════════════════════╝"
echo ""

GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

PASSED=0
FAILED=0
TARGET_TOPIC="target-topic-db-kafka"

# ============================================================================
# ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ
# ============================================================================
wait_for_engine() {
    local seconds=$1
    echo -ne "${YELLOW}   Ожидание обработки Engine"
    for i in $(seq 1 $seconds); do
        echo -ne "."
        sleep 1
    done
    echo -e "${NC}"
    sleep 20
}

reset_kafka_topic() {
    local topic=$1
    docker exec kafka33 kafka-topics --delete --topic "$topic" --bootstrap-server kafka33:9092 &>/dev/null
    sleep 2
    docker exec kafka33 kafka-topics --create \
        --topic "$topic" \
        --bootstrap-server kafka33:9092 \
        --partitions 1 \
        --replication-factor 1 &>/dev/null
    echo -e "${GREEN}   ✓ Топик '$topic' пересоздан${NC}"
    sleep 20
}

read_kafka_messages() {
    docker exec kafka33 kafka-console-consumer \
        --bootstrap-server kafka33:9092 \
        --topic "$TARGET_TOPIC" \
        --from-beginning \
        --max-messages 200 \
        --timeout-ms 10000 2>/dev/null
    sleep 20
}

get_source_count() {
    docker exec postgres-source psql -U admin -d source_db -t -c "SELECT COUNT(*) FROM source.data;" 2>/dev/null | tr -d ' \n'
    sleep 20
}

add_source_data() {
    local message=$1
    local value=$2
    docker exec postgres-source psql -U admin -d source_db -c "
        INSERT INTO source.data (payload) 
        VALUES ('{\"message\": \"$message\", \"value\": $value, \"timestamp\": \"$(date -Iseconds)\"}');" &>/dev/null
    sleep 20
}

# ============================================================================
# 0. ПРОВЕРКА ЗАВИСИМОСТЕЙ
# ============================================================================
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}🔧 0. ПРОВЕРКА ЗАВИСИМОСТЕЙ${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

echo -ne "${YELLOW}   Проверка Source Database...${NC}"
if docker exec postgres-source pg_isready -U admin -d source_db &>/dev/null; then
    echo -e "${GREEN} ✓ OK${NC}"
else
    echo -e "${RED} ✗ НЕ ДОСТУПНА${NC}"
    exit 1
fi

echo -ne "${YELLOW}   Проверка Kafka (kafka33)...${NC}"
if docker exec kafka33 kafka-broker-api-versions --bootstrap-server kafka33:9092 &>/dev/null; then
    echo -e "${GREEN} ✓ OK${NC}"
else
    echo -e "${RED} ✗ НЕ ДОСТУПЕН${NC}"
    exit 1
fi

# ============================================================================
# 1. ПОДГОТОВКА БАЗЫ ДАННЫХ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}📊 1. ПОДГОТОВКА БАЗЫ ДАННЫХ${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Создаём схему и таблицу
docker exec postgres-source psql -U admin -d source_db -c "CREATE SCHEMA IF NOT EXISTS source;" &>/dev/null
docker exec postgres-source psql -U admin -d source_db -c "
    CREATE TABLE IF NOT EXISTS source.data (
        id SERIAL PRIMARY KEY,
        payload JSONB NOT NULL,
        created_at TIMESTAMP DEFAULT NOW()
    );" &>/dev/null

# Очищаем
docker exec postgres-source psql -U admin -d source_db -c "TRUNCATE source.data RESTART IDENTITY CASCADE;" &>/dev/null

# Наполняем тестовыми данными
echo -e "${YELLOW}   Наполнение Source Database тестовыми данными...${NC}"
for i in {1..3}; do
    add_source_data "DB→Kafka Record $i" $((i*100))
done

SOURCE_COUNT=$(get_source_count)
echo -e "${GREEN}   ✓ Source Database: ${SOURCE_COUNT} записей${NC}"

# Подготовка Kafka топика
reset_kafka_topic "$TARGET_TOPIC"

# ============================================================================
# 2. ОЧИСТКА СТАРЫХ КОНФИГУРАЦИЙ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}🧹 2. ОЧИСТКА СТАРЫХ КОНФИГУРАЦИЙ${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

OLD_CONNECTIONS=$(curl -s http://localhost:5003/api/Subscription/connections | jq -r '.[] | select(.integrationPattern == "DatabaseToKafka") | .id' 2>/dev/null)
if [ -n "$OLD_CONNECTIONS" ]; then
    for id in $OLD_CONNECTIONS; do
        curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$id &>/dev/null
        echo -e "${YELLOW}   Удалена связь ID: $id${NC}"
    done
fi
echo -e "${GREEN}   ✓ Старые DatabaseToKafka связи удалены${NC}"
wait_for_engine 3

# ============================================================================
# 3. СОЗДАНИЕ ИНТЕРФЕЙСОВ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}🔌 3. СОЗДАНИЕ ИНТЕРФЕЙСОВ${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Source Database Interface
SOURCE_RESPONSE=$(curl -s -X POST http://localhost:5001/api/Publication/interfaces \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Source Database Interface",
    "productName": "SourceDBProduct",
    "interfaceType": 0,
    "description": "Source PostgreSQL database for batch replication to Kafka",
    "productType": 1,
    "host": "postgres-source",
    "port": "5432",
    "username": "admin",
    "password": "password",
    "databaseName": "source_db",
    "scheme": "source"
  }')

SOURCE_ID=$(echo $SOURCE_RESPONSE | jq -r '.interfaceId // .id // empty')
if [ -z "$SOURCE_ID" ] || [ "$SOURCE_ID" = "null" ]; then
    echo -e "${RED}✗ Failed to create Source Database Interface${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Source Database Interface ID: ${SOURCE_ID}${NC}"

# Target Kafka Interface
TARGET_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Interface \
  -H "Content-Type: application/json" \
  -d "{
    \"name\": \"Target Kafka Interface\",
    \"productName\": \"KafkaTargetProduct\",
    \"interfaceType\": 1,
    \"description\": \"Target Kafka topic for batch replication\",
    \"bootstrapServers\": \"kafka33:9092\",
    \"topicName\": \"$TARGET_TOPIC\",
    \"username\": \"\",
    \"password\": \"\"
  }")

TARGET_ID=$(echo $TARGET_RESPONSE | jq -r '.interfaceId // .id // empty')
if [ -z "$TARGET_ID" ] || [ "$TARGET_ID" = "null" ]; then
    echo -e "${RED}✗ Failed to create Target Kafka Interface${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Target Kafka Interface ID: ${TARGET_ID}${NC}"

# ============================================================================
# 4. ТЕСТ 1: ЕДИНОРАЗОВОЕ ВЫПОЛНЕНИЕ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}⚡ 4. ТЕСТ 1: ЕДИНОРАЗОВОЕ ВЫПОЛНЕНИЕ${NC}"
echo -e "${CYAN}   (schedule: * * * * * — выполняется сразу)${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

reset_kafka_topic "$TARGET_TOPIC"

CONNECT_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
  -H "Content-Type: application/json" \
  -d "{
    \"publicationInterfaceId\": $SOURCE_ID,
    \"subscriptionInterfaceId\": $TARGET_ID,
    \"integrationPattern\": 7,
    \"scheduleCron\": \"* * * * *\",
    \"maxRetryAttempts\": 3,
    \"retryDelaySeconds\": 30,
    \"executionTimeoutSeconds\": 300
  }")

CONFIG_ID=$(echo $CONNECT_RESPONSE | jq -r '.orchestrationConfigId // .id // empty')
echo -e "${GREEN}✓ Связь создана (Config ID: ${CONFIG_ID})${NC}"

echo -e "${YELLOW}   Ожидание обработки Engine (25 сек)...${NC}"
wait_for_engine 25

RESULT=$(read_kafka_messages)
MESSAGE_COUNT=$(echo "$RESULT" | grep -c "DB→Kafka Record" 2>/dev/null || echo "0")
echo -e "${YELLOW}   Сообщений в Kafka: ${MESSAGE_COUNT}${NC}"

if [ "$MESSAGE_COUNT" -eq 3 ]; then
    echo -e "${GREEN}   ✅ Единоразовое выполнение: УСПЕШНО!${NC}"
    ((PASSED++))
else
    echo -e "${RED}   ❌ Единоразовое выполнение: НЕ УСПЕШНО${NC}"
    ((FAILED++))
fi

curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$CONFIG_ID &>/dev/null
wait_for_engine 10

# ============================================================================
# 5. ТЕСТ 2: РАСПИСАНИЕ КАЖДУЮ МИНУТУ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}⏰ 5. ТЕСТ 2: РАСПИСАНИЕ КАЖДУЮ МИНУТУ${NC}"
echo -e "${CYAN}   (schedule: */1 * * * *)${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

reset_kafka_topic "$TARGET_TOPIC"

CONNECT_RESPONSE2=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
  -H "Content-Type: application/json" \
  -d "{
    \"publicationInterfaceId\": $SOURCE_ID,
    \"subscriptionInterfaceId\": $TARGET_ID,
    \"integrationPattern\": 7,
    \"scheduleCron\": \"*/1 * * * *\",
    \"maxRetryAttempts\": 3,
    \"retryDelaySeconds\": 30,
    \"executionTimeoutSeconds\": 300
  }")

CONFIG_ID2=$(echo $CONNECT_RESPONSE2 | jq -r '.orchestrationConfigId // .id // empty')
echo -e "${GREEN}✓ Связь с расписанием создана (Config ID: ${CONFIG_ID2})${NC}"

CURRENT_MIN=$(date +%M)
WAIT_SEC=$(( 60 - 10#$CURRENT_MIN + 10 ))
echo -e "${YELLOW}   Ожидание следующего выполнения расписания (~${WAIT_SEC} сек)...${NC}"
sleep $WAIT_SEC

echo -e "${YELLOW}   Добавление новых записей в Source Database...${NC}"
for i in {4..5}; do
    add_source_data "Scheduled DB→Kafka Record $i" $((i*100))
done
echo -e "${GREEN}   ✓ Добавлено 2 новые записи${NC}"

wait_for_engine 20

RESULT2=$(read_kafka_messages)
MESSAGE_COUNT2=$(echo "$RESULT2" | grep -c "DB→Kafka Record\|Scheduled DB→Kafka Record" 2>/dev/null || echo "0")
echo -e "${YELLOW}   Сообщений в Kafka: ${MESSAGE_COUNT2}${NC}"

if [ "$MESSAGE_COUNT2" -eq 5 ]; then
    echo -e "${GREEN}   ✅ Расписание: УСПЕШНО!${NC}"
    ((PASSED++))
else
    echo -e "${RED}   ❌ Расписание: НЕ УСПЕШНО${NC}"
    ((FAILED++))
fi

curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$CONFIG_ID2 &>/dev/null
wait_for_engine 5

# ============================================================================
# 6. ТЕСТ 3: МАССОВАЯ ЗАГРУЗКА (100 записей)
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}📦 6. ТЕСТ 3: МАССОВАЯ ЗАГРУЗКА (100 записей)${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

docker exec postgres-source psql -U admin -d source_db -c "TRUNCATE source.data RESTART IDENTITY CASCADE;" &>/dev/null
reset_kafka_topic "$TARGET_TOPIC"

echo -e "${YELLOW}   Генерация 100 тестовых записей...${NC}"
for i in {1..100}; do
    add_source_data "Bulk DB→Kafka Record $i" $((i*10))
    if [ $((i % 10)) -eq 0 ]; then
        echo -ne "\r   Прогресс: $i/100"
    fi
done
echo ""

SOURCE_COUNT=$(get_source_count)
echo -e "${GREEN}   ✓ Source Database: ${SOURCE_COUNT} записей${NC}"

CONNECT_RESPONSE3=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
  -H "Content-Type: application/json" \
  -d "{
    \"publicationInterfaceId\": $SOURCE_ID,
    \"subscriptionInterfaceId\": $TARGET_ID,
    \"integrationPattern\": 7,
    \"scheduleCron\": \"* * * * *\",
    \"maxRetryAttempts\": 3,
    \"retryDelaySeconds\": 30,
    \"executionTimeoutSeconds\": 300
  }")

CONFIG_ID3=$(echo $CONNECT_RESPONSE3 | jq -r '.orchestrationConfigId // .id // empty')
echo -e "${GREEN}✓ Связь создана (Config ID: ${CONFIG_ID3})${NC}"

wait_for_engine 30

RESULT3=$(read_kafka_messages)
MESSAGE_COUNT3=$(echo "$RESULT3" | grep -c "Bulk DB→Kafka Record" 2>/dev/null || echo "0")
echo -e "${YELLOW}   Сообщений в Kafka: ${MESSAGE_COUNT3}${NC}"

if [ "$MESSAGE_COUNT3" -eq 100 ]; then
    echo -e "${GREEN}   ✅ Массовая загрузка: УСПЕШНО!${NC}"
    ((PASSED++))
else
    echo -e "${RED}   ❌ Массовая загрузка: НЕ УСПЕШНО${NC}"
    ((FAILED++))
fi

# ============================================================================
# 7. ПРОВЕРКА ДАННЫХ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}🔍 7. ПРОВЕРКА ДАННЫХ${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

echo -e "${YELLOW}   Kafka топик (последние 5 сообщений):${NC}"
read_kafka_messages | tail -5

# ============================================================================
# 8. ИТОГИ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}📊 ИТОГИ ТЕСТИРОВАНИЯ${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

TOTAL=$((PASSED + FAILED))
if [ $FAILED -eq 0 ]; then
    echo -e "${GREEN}✅ Все тесты пройдены: ${PASSED}/${TOTAL}${NC}"
else
    echo -e "${RED}❌ Пройдено: ${PASSED}, Не пройдено: ${FAILED} из ${TOTAL}${NC}"
fi

# Финальная очистка
echo -e "\n${YELLOW}🧹 Финальная очистка...${NC}"
curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$CONFIG_ID3 &>/dev/null
docker exec postgres-source psql -U admin -d source_db -c "TRUNCATE source.data RESTART IDENTITY CASCADE;" &>/dev/null
reset_kafka_topic "$TARGET_TOPIC" &>/dev/null
echo -e "${GREEN}✓ Очистка завершена${NC}"

exit 0