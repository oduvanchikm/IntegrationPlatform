#!/bin/bash

echo "═══════════════════════════════════════════════════════════════════════════════"
echo "🚀 ПОЛНЫЙ НАГРУЗОЧНЫЙ ТЕСТ ДЛЯ ВСЕХ МЕТРИК (ВКЛЮЧАЯ ENGINE KAFKA)"
echo "═══════════════════════════════════════════════════════════════════════════════"

GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

# ============================================================================
# 1. ПОДГОТОВКА: ОЧИСТКА СТАРЫХ ДАННЫХ
# ============================================================================
echo -e "\n${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}🧹 1. ПОДГОТОВКА: ОЧИСТКА СТАРЫХ ДАННЫХ${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Удаляем старые тестовые интеграции
echo -e "${YELLOW}   Удаление старых тестовых интеграций...${NC}"
OLD_CONNECTIONS=$(curl -s http://localhost:5003/api/Subscription/connections | jq -r '.[] | select(.integrationPattern == 7 or .integrationPattern == 8) | .id')
for id in $OLD_CONNECTIONS; do
    curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$id &>/dev/null
    echo -ne "${GREEN}.${NC}"
done
echo -e " ${GREEN}✓ Старые интеграции удалены${NC}"

# Создаём топики для Kafka
echo -e "${YELLOW}   Создание топиков Kafka...${NC}"
docker exec kafka2 kafka-topics --create --topic source-topic --bootstrap-server kafka2:9092 --partitions 1 --replication-factor 1 2>/dev/null
docker exec kafka33 kafka-topics --create --topic target-topic --bootstrap-server kafka33:9092 --partitions 1 --replication-factor 1 2>/dev/null
docker exec kafka2 kafka-topics --create --topic source-topic-streaming --bootstrap-server kafka2:9092 --partitions 1 --replication-factor 1 2>/dev/null
echo -e "${GREEN}   ✓ Топики готовы${NC}"

# ============================================================================
# 2. СОЗДАНИЕ ИНТЕРФЕЙСОВ (Source и Target)
# ============================================================================
echo -e "\n${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}📦 2. СОЗДАНИЕ ИНТЕРФЕЙСОВ${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Source API интерфейсы
echo -e "${YELLOW}   Создание Source API интерфейсов...${NC}"
for i in {1..10}; do
    curl -s -X POST http://localhost:5001/api/Publication/interfaces \
      -H "Content-Type: application/json" \
      -d "{
        \"name\": \"LoadTestSource_API_$i\",
        \"productName\": \"LoadTestProduct\",
        \"interfaceType\": 2,
        \"description\": \"Нагрузочный тест API источник $i\",
        \"productType\": 1,
        \"host\": \"http://mock-api-source\",
        \"port\": \"8080\",
        \"endpoint\": \"/api/source-data\",
        \"username\": \"test\",
        \"password\": \"test\"
      }" > /dev/null
    echo -ne "${GREEN}.${NC}"
done
echo -e " ${GREEN}✓ 10 Source API интерфейсов создано${NC}"

# Source Kafka интерфейсы (для Kafka → Kafka тестов)
echo -e "${YELLOW}   Создание Source Kafka интерфейсов...${NC}"
SOURCE_KAFKA_RESPONSE=$(curl -s -X POST http://localhost:5001/api/Publication/interfaces \
  -H "Content-Type: application/json" \
  -d '{
    "name": "LoadTestSource_Kafka",
    "productName": "LoadTestKafkaProduct",
    "interfaceType": 1,
    "description": "Source Kafka для нагрузки",
    "productType": 1,
    "bootstrapServers": "kafka2:9092",
    "topicName": "source-topic",
    "username": "",
    "password": ""
  }')
SOURCE_KAFKA_ID=$(echo $SOURCE_KAFKA_RESPONSE | jq -r '.interfaceId')
echo -e "${GREEN}✓ Source Kafka интерфейс: ID=$SOURCE_KAFKA_ID${NC}"

# Target API интерфейсы
echo -e "${YELLOW}   Создание Target API интерфейсов...${NC}"
for i in {1..10}; do
    curl -s -X POST http://localhost:5003/api/Interface \
      -H "Content-Type: application/json" \
      -d "{
        \"name\": \"LoadTestTarget_API_$i\",
        \"productName\": \"LoadTestTargetProduct\",
        \"interfaceType\": 2,
        \"description\": \"Нагрузочный тест API получатель $i\",
        \"host\": \"http://mock-api-target\",
        \"port\": \"8080\",
        \"endpoint\": \"/api/target-data\",
        \"username\": \"test\",
        \"password\": \"test\"
      }" > /dev/null
    echo -ne "${GREEN}.${NC}"
done
echo -e " ${GREEN}✓ 10 Target API интерфейсов создано${NC}"

# Target Kafka интерфейсы (для Kafka → Kafka тестов)
echo -e "${YELLOW}   Создание Target Kafka интерфейсов...${NC}"
TARGET_KAFKA_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Interface \
  -H "Content-Type: application/json" \
  -d '{
    "name": "LoadTestTarget_Kafka",
    "productName": "LoadTestKafkaProduct",
    "interfaceType": 1,
    "description": "Target Kafka для нагрузки",
    "bootstrapServers": "kafka33:9092",
    "topicName": "target-topic",
    "username": "",
    "password": ""
  }')
TARGET_KAFKA_ID=$(echo $TARGET_KAFKA_RESPONSE | jq -r '.interfaceId')
echo -e "${GREEN}✓ Target Kafka интерфейс: ID=$TARGET_KAFKA_ID${NC}"

# ============================================================================
# 3. СОЗДАНИЕ ИНТЕГРАЦИЙ
# ============================================================================
echo -e "\n${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}🔗 3. СОЗДАНИЕ ИНТЕГРАЦИЙ${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# API → API интеграции
echo -e "${YELLOW}   Создание API → API интеграций...${NC}"
SOURCE_IDS=$(curl -s "http://localhost:5002/api/Search/interfaces/by-product?productName=LoadTestProduct" | jq -r '.[].id' | head -5)
TARGET_IDS=$(curl -s "http://localhost:5003/api/Subscription/interfaces" | jq -r '.[] | select(.productName=="LoadTestTargetProduct") | .id' | head -5)
SOURCE_ARRAY=($SOURCE_IDS)
TARGET_ARRAY=($TARGET_IDS)

for i in {0..4}; do
    if [ -n "${SOURCE_ARRAY[$i]}" ] && [ -n "${TARGET_ARRAY[$i]}" ]; then
        curl -s -X POST http://localhost:5003/api/Subscription/connect \
          -H "Content-Type: application/json" \
          -d "{
            \"publicationInterfaceId\": ${SOURCE_ARRAY[$i]},
            \"subscriptionInterfaceId\": ${TARGET_ARRAY[$i]},
            \"integrationPattern\": 7,
            \"scheduleCron\": \"*/5 * * * *\"
          }" > /dev/null
        echo -ne "${GREEN}.${NC}"
    fi
done
echo -e " ${GREEN}✓ 5 API → API интеграций создано${NC}"

# Kafka → Kafka интеграция (ВАЖНО для produced метрики!)
echo -e "${YELLOW}   Создание Kafka → Kafka интеграции...${NC}"
if [ -n "$SOURCE_KAFKA_ID" ] && [ -n "$TARGET_KAFKA_ID" ] && [ "$SOURCE_KAFKA_ID" != "null" ] && [ "$TARGET_KAFKA_ID" != "null" ]; then
    CONNECT_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
      -H "Content-Type: application/json" \
      -d "{
        \"publicationInterfaceId\": $SOURCE_KAFKA_ID,
        \"subscriptionInterfaceId\": $TARGET_KAFKA_ID,
        \"integrationPattern\": 8,
        \"scheduleCron\": \"* * * * *\"
      }")
    CONFIG_ID=$(echo $CONNECT_RESPONSE | jq -r '.orchestrationConfigId')
    echo -e "${GREEN}✓ Kafka → Kafka интеграция создана: Config ID=$CONFIG_ID${NC}"
else
    echo -e "${RED}⚠ Не удалось создать Kafka → Kafka интеграцию${NC}"
fi

# ============================================================================
# 4. ПОИСКОВЫЕ ЗАПРОСЫ (Search API)
# ============================================================================
echo -e "\n${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}🔎 4. ПОИСКОВЫЕ ЗАПРОСЫ${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

for i in {1..100}; do
    curl -s "http://localhost:5002/api/Search/interfaces/by-product?productName=LoadTest" > /dev/null
    curl -s "http://localhost:5002/api/Search/interfaces/by-type?interfaceType=2" > /dev/null
    curl -s "http://localhost:5002/api/Search/interfaces/by-interface?interfaceName=LoadTest" > /dev/null
    if [ $((i % 20)) -eq 0 ]; then
        echo -ne "${GREEN}.${NC}"
    fi
done
echo -e " ${GREEN}✓ 300 поисковых запросов выполнено${NC}"

# ============================================================================
# 5. ОТПРАВКА ДАННЫХ В API (RPS)
# ============================================================================
echo -e "\n${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}📤 5. ОТПРАВКА ДАННЫХ В API (RPS)${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

for i in {1..200}; do
    curl -s -X POST http://localhost:5100/api/source-data \
      -H "Content-Type: application/json" \
      -d "{\"id\": $i, \"message\": \"Load test message $i\", \"value\": $i}" > /dev/null
    if [ $((i % 50)) -eq 0 ]; then
        echo -ne "${GREEN}.${NC}"
    fi
done
echo -e " ${GREEN}✓ 200 запросов отправлено в Mock API${NC}"

# ============================================================================
# 6. ОТПРАВКА СООБЩЕНИЙ В KAFKA (для Engine consumption)
# ============================================================================
echo -e "\n${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}📨 6. ОТПРАВКА СООБЩЕНИЙ В KAFKA (consumption)${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

for i in {1..100}; do
    echo "Kafka consumption test message $i for Engine" | \
      docker exec -i kafka2 kafka-console-producer \
      --bootstrap-server kafka2:9092 \
      --topic source-topic 2>/dev/null
    if [ $((i % 20)) -eq 0 ]; then
        echo -ne "${GREEN}.${NC}"
    fi
done
echo -e " ${GREEN}✓ 100 сообщений отправлено в source-topic${NC}"

# ============================================================================
# 7. ОТПРАВКА В target-topic (прямая, для проверки)
# ============================================================================
echo -e "\n${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}📨 7. ОТПРАВКА В target-topic (прямая)${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

for i in {1..50}; do
    echo "Direct message $i to target-topic" | \
      docker exec -i kafka33 kafka-console-producer \
      --bootstrap-server kafka33:9092 \
      --topic target-topic 2>/dev/null
    if [ $((i % 20)) -eq 0 ]; then
        echo -ne "${GREEN}.${NC}"
    fi
done
echo -e " ${GREEN}✓ 50 сообщений отправлено напрямую в target-topic${NC}"

# ============================================================================
# 8. СТРИМИНГОВЫЕ СООБЩЕНИЯ
# ============================================================================
echo -e "\n${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}📨 8. СТРИМИНГОВЫЕ СООБЩЕНИЯ В KAFKA${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

for i in {1..100}; do
    echo "{\"id\": $i, \"message\": \"Streaming test $i\", \"timestamp\": \"$(date -Iseconds)\"}" | \
      docker exec -i kafka2 kafka-console-producer \
      --bootstrap-server kafka2:9092 \
      --topic source-topic 2>/dev/null
    if [ $((i % 20)) -eq 0 ]; then
        echo -ne "${GREEN}.${NC}"
    fi
done
echo -e " ${GREEN}✓ 100 стриминговых сообщений отправлено${NC}"

# ============================================================================
# 9. ИНТЕНСИВНАЯ НАГРУЗКА НА API (RPS)
# ============================================================================
echo -e "\n${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}⚡ 9. ИНТЕНСИВНАЯ НАГРУЗКА НА API${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

for i in {1..300}; do
    curl -s http://localhost:5001/api/Publication/test-connection > /dev/null &
    curl -s http://localhost:5002/api/Search/interfaces/by-product?productName= > /dev/null &
    curl -s http://localhost:5003/api/Subscription/health > /dev/null &
    if [ $((i % 30)) -eq 0 ]; then
        wait
        echo -ne "${GREEN}.${NC}"
    fi
done
wait
echo -e " ${GREEN}✓ 900 параллельных запросов выполнено${NC}"

# ============================================================================
# 10. ПРОВЕРКА ЛОГОВ ENGINE
# ============================================================================
echo -e "\n${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}📋 10. ПРОВЕРКА ЛОГОВ ENGINE${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

echo -e "${YELLOW}   Последние логи Kafka в Engine:${NC}"
docker logs integration-engine --tail 30 2>&1 | grep -E "KAFKA|MESSAGE RECEIVED|WriteToKafka|consumed|produced" | tail -10

# ============================================================================
# 11. ПРОВЕРКА target-topic (сообщения от Engine)
# ============================================================================
echo -e "\n${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}🔍 11. ПРОВЕРКА target-topic (должны быть сообщения от Engine)${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

docker exec kafka33 kafka-console-consumer \
  --bootstrap-server kafka33:9092 \
  --topic target-topic \
  --from-beginning \
  --max-messages 5 \
  --timeout-ms 5000 2>/dev/null | head -5

# ============================================================================
# 12. ПРОВЕРКА МЕТРИК
# ============================================================================
echo -e "\n${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}📊 12. ТЕКУЩИЕ ЗНАЧЕНИЯ МЕТРИК${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

echo -e "${YELLOW}   publication_active_source_interfaces:${NC}"
curl -s 'http://localhost:9090/api/v1/query?query=publication_active_source_interfaces' | jq '.data.result[0].value[1]'

echo -e "${YELLOW}   subscriptions_active_total:${NC}"
curl -s 'http://localhost:9090/api/v1/query?query=subscriptions_active_total' | jq '.data.result[0].value[1]'

echo -e "${YELLOW}   subscription_orchestration_configs_total:${NC}"
curl -s 'http://localhost:9090/api/v1/query?query=subscription_orchestration_configs_total' | jq '.data.result[0].value[1]'

echo -e "${YELLOW}   engine_events_processed_total:${NC}"
curl -s 'http://localhost:9090/api/v1/query?query=engine_events_processed_total' | jq '.data.result'

echo -e "${YELLOW}   engine_kafka_messages_consumed_total:${NC}"
curl -s 'http://localhost:9090/api/v1/query?query=engine_kafka_messages_consumed_total' | jq '.data.result'

echo -e "${YELLOW}   engine_kafka_messages_produced_total:${NC}"
curl -s 'http://localhost:9090/api/v1/query?query=engine_kafka_messages_produced_total' | jq '.data.result'

echo -e "${YELLOW}   RPS (rate(http_requests_received_total[1m])):${NC}"
curl -s 'http://localhost:9090/api/v1/query?query=rate(http_requests_received_total[1m])' | jq '.data.result[0].value[1]'

# ============================================================================
# 13. ВСЕ МЕТРИКИ ENGINE
# ============================================================================
echo -e "\n${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}⚙️ 13. ВСЕ МЕТРИКИ ENGINE ИЗ /metrics${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

curl -s http://localhost:9091/metrics | grep "engine_" | grep -E "consumed|produced|events|active" | head -15

# ============================================================================
# 14. ИТОГИ
# ============================================================================
echo -e "\n${GREEN}═══════════════════════════════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}✅ ПОЛНЫЙ НАГРУЗОЧНЫЙ ТЕСТ ЗАВЕРШЕН${NC}"
echo -e "${GREEN}═══════════════════════════════════════════════════════════════════════════════${NC}"
echo ""
echo "📊 Открой Grafana: http://localhost:3000"
echo "   Логин: admin, пароль: admin"
echo "   Дашборд: Integration Platform Dashboard"
echo ""
echo "📈 Ожидаемые значения метрик:"
echo "   • publication_active_source_interfaces → 11+"
echo "   • subscriptions_active_total → 11+"
echo "   • subscription_orchestration_configs_total → 6+"
echo "   • engine_events_processed_total → ДОЛЖНО БЫТЬ >0"
echo "   • engine_kafka_messages_consumed_total → ДОЛЖНО БЫТЬ >100"
echo "   • engine_kafka_messages_produced_total → ДОЛЖНО БЫТЬ >0"
echo ""
echo "⏳ Подожди 1-2 минуты, чтобы все метрики обновились в Grafana"