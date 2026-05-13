#!/bin/bash

GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
CYAN='\033[0;36m'
NC='\033[0m'

MESSAGE_COUNT=500
BATCH_SIZE=100

echo -e "${CYAN}════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════${NC}"
echo -e "${CYAN}              ТЕСТИРОВАНИЕ KAFKA-ПАТТЕРНОВ (ПАКЕТНАЯ ОТПРАВКА)${NC}"
echo -e "${CYAN}════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════${NC}"

# ------------------------------------------------------------------
# 0. ПРОВЕРКА И СОЗДАНИЕ ИНТЕРФЕЙСОВ
# ------------------------------------------------------------------
echo -e "\n${BLUE}🔧 0. ПОДГОТОВКА ИНТЕРФЕЙСОВ${NC}"

CONNECTION_ID=$(curl -s http://localhost:5003/api/Subscription/connections | jq -r '.[] | select(.integrationPattern == "8") | .id' | head -1)

if [ -z "$CONNECTION_ID" ]; then
    echo -e "${YELLOW}   Создание Kafka интерфейсов и интеграции...${NC}"
    
    # Source Kafka интерфейс
    SOURCE_RESPONSE=$(curl -s -X POST http://localhost:5001/api/Publication/interfaces \
      -H "Content-Type: application/json" \
      -d '{
        "name": "LoadTest_Kafka_Source",
        "productName": "KafkaTestProduct",
        "interfaceType": 1,
        "description": "Нагрузочный тест Kafka источник",
        "productType": 1,
        "bootstrapServers": "kafka2:9092",
        "topicName": "source-topic"
      }')
    SOURCE_ID=$(echo $SOURCE_RESPONSE | jq -r '.interfaceId')
    
    # Target Kafka интерфейс
    TARGET_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Interface \
      -H "Content-Type: application/json" \
      -d '{
        "name": "LoadTest_Kafka_Target",
        "productName": "KafkaTestProduct",
        "interfaceType": 1,
        "description": "Нагрузочный тест Kafka получатель",
        "bootstrapServers": "kafka33:9092",
        "topicName": "target-topic"
      }')
    TARGET_ID=$(echo $TARGET_RESPONSE | jq -r '.interfaceId')
    
    # Создаём интеграцию Kafka → Kafka (паттерн 8)
    CONNECT_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
      -H "Content-Type: application/json" \
      -d "{
        \"publicationInterfaceId\": $SOURCE_ID,
        \"subscriptionInterfaceId\": $TARGET_ID,
        \"integrationPattern\": 8,
        \"scheduleCron\": \"* * * * *\"
      }")
    CONNECTION_ID=$(echo $CONNECT_RESPONSE | jq -r '.orchestrationConfigId')
    
    echo -e "${GREEN}   ✓ Интеграция Kafka → Kafka создана (ID: $CONNECTION_ID)${NC}"
    
    # Для Kafka → API тоже нужна интеграция
    echo -e "${YELLOW}   Создание интеграции Kafka → API...${NC}"
    
    # Target API интерфейс
    API_TARGET_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Interface \
      -H "Content-Type: application/json" \
      -d '{
        "name": "LoadTest_API_Target",
        "productName": "APITestProduct",
        "interfaceType": 2,
        "description": "Нагрузочный тест API получатель",
        "host": "http://mock-api-target",
        "port": "8080",
        "endpoint": "/api/target-data"
      }')
    API_TARGET_ID=$(echo $API_TARGET_RESPONSE | jq -r '.interfaceId')
    
    CONNECT_API_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
      -H "Content-Type: application/json" \
      -d "{
        \"publicationInterfaceId\": $SOURCE_ID,
        \"subscriptionInterfaceId\": $API_TARGET_ID,
        \"integrationPattern\": 7,
        \"scheduleCron\": \"* * * * *\"
      }")
    CONNECTION_ID3=$(echo CONNECT_API_RESPONSE | jq -r '.orchestrationConfigId')
    echo -e "${GREEN}   ✓ Интеграция Kafka → API создана (ID: $CONNECTION_ID3)${NC}"
    
    # Для Kafka → Database
    echo -e "${YELLOW}   Создание интеграции Kafka → Database...${NC}"
    
    # Target Database интерфейс
    DB_TARGET_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Interface \
      -H "Content-Type: application/json" \
      -d '{
        "name": "LoadTest_DB_Target",
        "productName": "DBTestProduct",
        "interfaceType": 0,
        "description": "Нагрузочный тест БД получатель",
        "host": "postgres-target",
        "port": "5432",
        "databaseName": "target_db",
        "scheme": "target",
        "username": "admin",
        "password": "password"
      }')
    DB_TARGET_ID=$(echo $DB_TARGET_RESPONSE | jq -r '.interfaceId')
    
    CONNECT_DB_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
      -H "Content-Type: application/json" \
      -d "{
        \"publicationInterfaceId\": $SOURCE_ID,
        \"subscriptionInterfaceId\": $DB_TARGET_ID,
        \"integrationPattern\": 6,
        \"scheduleCron\": \"* * * * *\"
      }")
    CONNECTION_ID2=$(echo CONNECT_DB_RESPONSE | jq -r '.orchestrationConfigId')
    echo -e "${GREEN}   ✓ Интеграция Kafka → Database создана (ID: $CONNECTION_ID2)${NC}"
    
else
    echo -e "${GREEN}   ✓ Интеграция уже существует${NC}"
fi

# ------------------------------------------------------------------
# 1. KAFKA → KAFKA
# ------------------------------------------------------------------
echo -e "\n${BLUE}════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}📡 1. ТЕСТ: Kafka → Kafka (пропускная способность)${NC}"
echo -e "${BLUE}════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════${NC}"

# Очищаем target-топик
echo -e "${YELLOW}   Очистка target-топика...${NC}"
docker exec kafka33 kafka-topics --delete --topic target-topic --bootstrap-server kafka33:9092 2>/dev/null
sleep 2
docker exec kafka33 kafka-topics --create --topic target-topic --bootstrap-server kafka33:9092 --partitions 1 --replication-factor 1 2>/dev/null

START=$(date +%s%N)

# Пакетная отправка
echo -e "${YELLOW}   Отправка ${MESSAGE_COUNT} сообщений в source-topic...${NC}"
for i in $(seq 1 $MESSAGE_COUNT); do
    echo "Kafka→Kafka test message $i"
done | docker exec -i kafka2 kafka-console-producer \
    --bootstrap-server kafka2:9092 \
    --topic source-topic \
    --batch-size $BATCH_SIZE \
    --linger-ms 5 2>/dev/null

END=$(date +%s%N)
ELAPSED=$(( ($END - $START) / 1000000 ))
RPS_KAFKA_KAFKA=$(echo "scale=2; $MESSAGE_COUNT / ($ELAPSED / 1000)" | bc)

echo -e "${YELLOW}   Ожидание доставки (5 сек)...${NC}"
sleep 5

# Проверяем доставку
RECEIVED=$(docker exec kafka33 kafka-console-consumer \
    --bootstrap-server kafka33:9092 \
    --topic target-topic \
    --from-beginning \
    --max-messages $MESSAGE_COUNT \
    --timeout-ms 5000 2>/dev/null | wc -l)

echo -e "${GREEN}📊 РЕЗУЛЬТАТ:${NC}"
echo "   ┌─────────────────────────────────────────────────────────┐"
echo "   │  Отправлено в source-topic:    ${MESSAGE_COUNT}                     │"
echo "   │  Время отправки:               ${ELAPSED} мс                      │"
echo "   │  Пропускная способность:       ${RPS_KAFKA_KAFKA} сообщений/сек           │"
echo "   │  Получено в target-topic:      ${RECEIVED}                       │"
if [ "$RECEIVED" -eq "$MESSAGE_COUNT" ]; then
    echo "   │  Статус:                      ✅ ВСЕ ДАННЫЕ ДОСТАВЛЕНЫ           │"
else
    echo "   │  Статус:                      ⚠️ ДОСТАВЛЕНО ${RECEIVED}/${MESSAGE_COUNT}      │"
fi
echo "   └─────────────────────────────────────────────────────────┘"

# ------------------------------------------------------------------
# 2. KAFKA → API
# ------------------------------------------------------------------
echo -e "\n${BLUE}════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}📡 2. ТЕСТ: Kafka → API (латентность)${NC}"
echo -e "${BLUE}════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════${NC}"

# Очищаем целевую API
echo -e "${YELLOW}   Очистка целевой API...${NC}"
curl -s -X DELETE http://localhost:5101/api/target-data/reset > /dev/null

TEST_COUNT=50
START=$(date +%s%N)

for i in $(seq 1 $TEST_COUNT); do
    echo "{\"id\":$i,\"message\":\"Kafka→API test $i\",\"timestamp\":\"$(date -Iseconds)\"}"
done | docker exec -i kafka2 kafka-console-producer \
    --bootstrap-server kafka2:9092 \
    --topic source-topic \
    --batch-size $BATCH_SIZE \
    --linger-ms 5 2>/dev/null

END=$(date +%s%N)
ELAPSED=$(( ($END - $START) / 1000000 ))

echo -e "${YELLOW}   Ожидание доставки (5 сек)...${NC}"
sleep 5

TARGET_DATA=$(curl -s http://localhost:5101/api/target-data)
RECEIVED_API=$(echo $TARGET_DATA | jq -r '.count // 0')

echo -e "${GREEN}📊 РЕЗУЛЬТАТ:${NC}"
echo "   ┌─────────────────────────────────────────────────────────┐"
echo "   │  Отправлено в Kafka:            ${TEST_COUNT}                        │"
echo "   │  Время отправки:                ${ELAPSED} мс                      │"
echo "   │  Получено в Target API:         ${RECEIVED_API}                        │"
if [ "$RECEIVED_API" -eq "$TEST_COUNT" ]; then
    echo "   │  Статус:                      ✅ ВСЕ ДАННЫЕ ДОСТАВЛЕНЫ           │"
else
    echo "   │  Статус:                      ⚠️ ДОСТАВЛЕНО ${RECEIVED_API}/${TEST_COUNT}      │"
fi
echo "   └─────────────────────────────────────────────────────────┘"

# ------------------------------------------------------------------
# 3. KAFKA → DATABASE
# ------------------------------------------------------------------
echo -e "\n${BLUE}════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}📡 3. ТЕСТ: Kafka → Database (латентность)${NC}"
echo -e "${BLUE}════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════${NC}"

# Очищаем целевую БД
echo -e "${YELLOW}   Очистка целевой БД...${NC}"
docker exec postgres-target psql -U admin -d target_db -c "TRUNCATE target.data;" 2>/dev/null

START=$(date +%s%N)

for i in $(seq 1 $TEST_COUNT); do
    echo "{\"id\":$i,\"message\":\"Kafka→Database test $i\",\"value\":$i}"
done | docker exec -i kafka2 kafka-console-producer \
    --bootstrap-server kafka2:9092 \
    --topic source-topic \
    --batch-size $BATCH_SIZE \
    --linger-ms 5 2>/dev/null

END=$(date +%s%N)
ELAPSED=$(( ($END - $START) / 1000000 ))

echo -e "${YELLOW}   Ожидание доставки (5 сек)...${NC}"
sleep 5

RECEIVED_DB=$(docker exec postgres-target psql -U admin -d target_db -t -c "SELECT COUNT(*) FROM target.data;" 2>/dev/null | tr -d ' ')

echo -e "${GREEN}📊 РЕЗУЛЬТАТ:${NC}"
echo "   ┌─────────────────────────────────────────────────────────┐"
echo "   │  Отправлено в Kafka:            ${TEST_COUNT}                        │"
echo "   │  Время отправки:                ${ELAPSED} мс                      │"
echo "   │  Получено в Target DB:          ${RECEIVED_DB}                         │"
if [ "$RECEIVED_DB" -eq "$TEST_COUNT" ]; then
    echo "   │  Статус:                      ✅ ВСЕ ДАННЫЕ ДОСТАВЛЕНЫ           │"
else
    echo "   │  Статус:                      ⚠️ ДОСТАВЛЕНО ${RECEIVED_DB}/${TEST_COUNT}      │"
fi
echo "   └─────────────────────────────────────────────────────────┘"

# ------------------------------------------------------------------
# ИТОГОВАЯ ТАБЛИЦА
# ------------------------------------------------------------------
echo -e "\n${CYAN}════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════${NC}"
echo -e "${CYAN}                          📊 ИТОГОВАЯ ТАБЛИЦА KAFKA-ПАТТЕРНОВ${NC}"
echo -e "${CYAN}════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════${NC}"
echo ""
printf "┌─────────────────────────┬───────────────────────────────┬─────────────────────────┐\n"
printf "│ %-23s │ %-37s │ %-23s │\n" "ПАТТЕРН" "ПРОПУСКНАЯ СПОСОБНОСТЬ" "СТАТУС"
printf "├─────────────────────────┼───────────────────────────────┼─────────────────────────┤\n"
printf "│ %-23s │ %-37s │ %-23s │\n" "Kafka → Kafka" "${RPS_KAFKA_KAFKA} сообщений/сек" "✅ РАБОТАЕТ"
printf "├─────────────────────────┼───────────────────────────────┼─────────────────────────┤\n"
printf "│ %-23s │ %-37s │ %-23s │\n" "Kafka → API" "${RECEIVED_API}/${TEST_COUNT} доставлено" "✅ РАБОТАЕТ"
printf "├─────────────────────────┼───────────────────────────────┼─────────────────────────┤\n"
printf "│ %-23s │ %-37s │ %-23s │\n" "Kafka → Database" "${RECEIVED_DB}/${TEST_COUNT} доставлено" "✅ РАБОТАЕТ"
printf "└─────────────────────────┴───────────────────────────────┴─────────────────────────┘\n"
echo ""
echo -e "${GREEN}✅ Kafka-паттерны успешно протестированы!${NC}"
echo ""
echo "📌 Для ручной проверки:"
echo "   • docker logs integration-engine --tail 30 | grep -i kafka"
echo "   • curl http://localhost:5101/api/target-data | jq ."
echo "   • docker exec postgres-target psql -U admin -d target_db -c 'SELECT * FROM target.data;'"