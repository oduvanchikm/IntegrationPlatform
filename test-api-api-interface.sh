#!/bin/bash

# ==========================================
# НАГРУЗОЧНОЕ ТЕСТИРОВАНИЕ KAFKA → KAFKA
# ==========================================

GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
CYAN='\033[0;36m'
NC='\033[0m'

MESSAGE_COUNT=1000
BATCH_SIZE=100

echo -e "${CYAN}════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════${NC}"
echo -e "${CYAN}                   НАГРУЗОЧНОЕ ТЕСТИРОВАНИЕ KAFKA → KAFKA${NC}"
echo -e "${CYAN}════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════${NC}"

# ------------------------------------------------------------------
# 1. ПРОВЕРКА ИНТЕРФЕЙСОВ
# ------------------------------------------------------------------
echo -e "\n${BLUE}🔧 1. ПРОВЕРКА ИНТЕРФЕЙСОВ${NC}"

# Проверяем Source интерфейс
SOURCE_ID=$(curl -s "http://localhost:5002/api/Search/interfaces/by-product?productName=KafkaSourceProduct" | jq -r '.[0].id // empty')
if [ -z "$SOURCE_ID" ]; then
    echo -e "${YELLOW}   Создание Source интерфейса...${NC}"
    SOURCE_RESPONSE=$(curl -s -X POST http://localhost:5001/api/Publication/interfaces \
      -H "Content-Type: application/json" \
      -d '{
        "name": "LoadTest_Kafka_Source",
        "productName": "KafkaSourceProduct",
        "interfaceType": 1,
        "productType": 1,
        "bootstrapServers": "kafka2:9092",
        "topicName": "source-topic"
      }')
    SOURCE_ID=$(echo $SOURCE_RESPONSE | jq -r '.interfaceId')
fi
echo -e "${GREEN}   ✓ Source интерфейс (ID: $SOURCE_ID)${NC}"

# Проверяем Target интерфейс
TARGET_ID=$(curl -s "http://localhost:5003/api/Subscription/interfaces" | jq -r '.[] | select(.name=="LoadTest_Kafka_Target") | .id // empty')
if [ -z "$TARGET_ID" ]; then
    echo -e "${YELLOW}   Создание Target интерфейса...${NC}"
    TARGET_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Interface \
      -H "Content-Type: application/json" \
      -d '{
        "name": "LoadTest_Kafka_Target",
        "productName": "KafkaTargetProduct",
        "interfaceType": 1,
        "bootstrapServers": "kafka33:9092",
        "topicName": "target-topic"
      }')
    TARGET_ID=$(echo $TARGET_RESPONSE | jq -r '.interfaceId')
fi
echo -e "${GREEN}   ✓ Target интерфейс (ID: $TARGET_ID)${NC}"

# ------------------------------------------------------------------
# 2. ПРОВЕРКА ИНТЕГРАЦИИ
# ------------------------------------------------------------------
echo -e "\n${BLUE}🔗 2. ПРОВЕРКА ИНТЕГРАЦИИ${NC}"

CONNECTION_ID=$(curl -s http://localhost:5003/api/Subscription/connections | jq -r '.[] | select(.integrationPattern == "8" and .interfacePublicationId == '$SOURCE_ID') | .id')
if [ -z "$CONNECTION_ID" ]; then
    echo -e "${YELLOW}   Создание интеграции Kafka → Kafka...${NC}"
    CONNECT_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
      -H "Content-Type: application/json" \
      -d "{
        \"publicationInterfaceId\": $SOURCE_ID,
        \"subscriptionInterfaceId\": $TARGET_ID,
        \"integrationPattern\": 8,
        \"scheduleCron\": \"* * * * *\"
      }")
    CONNECTION_ID=$(echo $CONNECT_RESPONSE | jq -r '.orchestrationConfigId')
fi
echo -e "${GREEN}   ✓ Интеграция существует (ID: $CONNECTION_ID)${NC}"

# ------------------------------------------------------------------
# 3. ОЧИСТКА ТОПИКОВ
# ------------------------------------------------------------------
echo -e "\n${BLUE}🗑️ 3. ОЧИСТКА ТОПИКОВ${NC}"

echo -e "${YELLOW}   Очистка target-топика...${NC}"
docker exec kafka33 kafka-topics --delete --topic target-topic --bootstrap-server kafka33:9092 2>/dev/null
sleep 2
docker exec kafka33 kafka-topics --create --topic target-topic --bootstrap-server kafka33:9092 --partitions 1 --replication-factor 1 2>/dev/null
echo -e "${GREEN}   ✓ Target топик очищен${NC}"

# ------------------------------------------------------------------
# 4. НАГРУЗОЧНЫЙ ТЕСТ
# ------------------------------------------------------------------
echo -e "\n${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}📊 4. ЗАПУСК НАГРУЗОЧНОГО ТЕСТА${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

START_TIME=$(date +%s%N)

echo -e "${YELLOW}   Отправка ${MESSAGE_COUNT} сообщений в source-topic...${NC}"

for i in $(seq 1 $MESSAGE_COUNT); do
    echo "Load test message $i"
done | docker exec -i kafka2 kafka-console-producer \
    --bootstrap-server kafka2:9092 \
    --topic source-topic \
    --batch-size $BATCH_SIZE \
    --linger-ms 5 2>/dev/null

END_TIME=$(date +%s%N)
ELAPSED_MS=$(( ($END_TIME - $START_TIME) / 1000000 ))
RPS=$(echo "scale=2; $MESSAGE_COUNT / ($ELAPSED_MS / 1000)" | bc)

echo -e "${GREEN}   ✅ Отправлено ${MESSAGE_COUNT} сообщений за ${ELAPSED_MS} мс${NC}"
echo -e "${GREEN}   ✅ Пропускная способность: ${RPS} сообщений/сек${NC}"

# ------------------------------------------------------------------
# 5. ПРОВЕРКА ДОСТАВКИ
# ------------------------------------------------------------------
echo -e "\n${BLUE}🔍 5. ПРОВЕРКА ДОСТАВКИ В TARGET-ТОПИК${NC}"
echo -e "${YELLOW}   Ожидание обработки Engine (20 сек)...${NC}"
sleep 20

# Проверяем количество сообщений в target-топике
RECEIVED_RAW=$(docker exec kafka33 kafka-console-consumer \
    --bootstrap-server kafka33:9092 \
    --topic target-topic \
    --from-beginning \
    --max-messages $MESSAGE_COUNT \
    --timeout-ms 50000 2>&1)

RECEIVED_COUNT=$(echo "$RECEIVED_RAW" | grep -v "Processed a total of" | grep -v "ERROR" | grep -v "^$" | wc -l)
    
echo -e "${GREEN}📊 РЕЗУЛЬТАТ:${NC}"
echo "   ┌─────────────────────────────────────────────────────────┐"
echo "   │  Отправлено сообщений:      ${MESSAGE_COUNT}                     │"
echo "   │  Пропускная способность:    ${RPS} сообщений/сек              │"
echo "   │  Получено в target-топике:  ${RECEIVED_COUNT}                       │"
if [ "$RECEIVED_COUNT" -eq "$MESSAGE_COUNT" ]; then
    echo "   │  Статус:                   ✅ ВСЕ ДАННЫЕ ДОСТАВЛЕНЫ           │"
else
    echo "   │  Статус:                   ⚠️ ДОСТАВЛЕНО ${RECEIVED_COUNT}/${MESSAGE_COUNT}      │"
fi
echo "   └─────────────────────────────────────────────────────────┘"

# ------------------------------------------------------------------
# 6. ПРОВЕРКА ЛОГОВ ENGINE
# ------------------------------------------------------------------
echo -e "\n${BLUE}📋 6. ПОСЛЕДНИЕ ЛОГИ ENGINE${NC}"
docker logs integration-engine --tail 15 2>/dev/null | grep -E "MESSAGE RECEIVED|WriteToKafka|KAFKA" | tail -5

# ------------------------------------------------------------------
# ИТОГИ
# ------------------------------------------------------------------
echo -e "\n${CYAN}════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════${NC}"
echo -e "${CYAN}                          📊 ИТОГИ ТЕСТИРОВАНИЯ KAFKA → KAFKA${NC}"
echo -e "${CYAN}════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════${NC}"
echo ""
echo -e "${GREEN}✅ Результаты:${NC}"
echo "   • Отправлено: ${MESSAGE_COUNT} сообщений"
echo "   • Пропускная способность: ${RPS} msg/s"
echo "   • Доставлено: ${RECEIVED_COUNT} сообщений"
echo ""
echo "📌 Для ручной проверки:"
echo "   • docker logs integration-engine --tail 30 | grep -i kafka"
echo "   • docker exec kafka33 kafka-console-consumer --bootstrap-server kafka33:9092 --topic target-topic --from-beginning --max-messages 5"