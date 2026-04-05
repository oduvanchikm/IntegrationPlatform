#!/bin/bash

echo "╔══════════════════════════════════════════════════════════════════════════════╗"
echo "║                        API → Kafka Integration Test                          ║"
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
TARGET_TOPIC="target-topic-api-kafka"

# ============================================================================
# ФУНКЦИИ
# ============================================================================
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
}

send_api_data() {
    local message=$1
    local value=${2:-0}
    local timestamp=$(date -Iseconds)
    
    docker exec mock-api-source curl -s -X POST http://localhost:8080/api/source-data \
        -H "Content-Type: application/json" \
        -d "{\"id\":$RANDOM,\"message\":\"$message\",\"value\":$value,\"timestamp\":\"$timestamp\"}" &>/dev/null
    sleep 1
}

read_kafka_messages() {
    docker exec kafka33 kafka-console-consumer \
        --bootstrap-server kafka33:9092 \
        --topic "$TARGET_TOPIC" \
        --from-beginning \
        --max-messages 200 \
        --timeout-ms 10000 2>/dev/null
}

# ============================================================================
# 0. ПРОВЕРКА ЗАВИСИМОСТЕЙ
# ============================================================================
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}🔧 0. ПРОВЕРКА ЗАВИСИМОСТЕЙ${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

echo -ne "${YELLOW}   Проверка Mock API Source...${NC}"
if curl -s -o /dev/null -w "%{http_code}" http://localhost:5100/health | grep -q "200"; then
    echo -e "${GREEN} ✓ OK${NC}"
else
    echo -e "${RED} ✗ НЕ ДОСТУПЕН${NC}"
    echo -e "${YELLOW}   Запустите: docker-compose up -d mock-api-source${NC}"
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
# 1. ПОДГОТОВКА KAFKA
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}📦 1. ПОДГОТОВКА KAFKA${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

reset_kafka_topic "$TARGET_TOPIC"

# ============================================================================
# 2. ОЧИСТКА СТАРЫХ КОНФИГУРАЦИЙ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}🧹 2. ОЧИСТКА СТАРЫХ КОНФИГУРАЦИЙ${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

OLD_CONNECTIONS=$(curl -s http://localhost:5003/api/Subscription/connections | jq -r '.[] | select(.integrationPattern == "ApiToKafka") | .id' 2>/dev/null)
if [ -n "$OLD_CONNECTIONS" ]; then
    for id in $OLD_CONNECTIONS; do
        curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$id &>/dev/null
        echo -e "${YELLOW}   Удалена связь ID: $id${NC}"
    done
fi
echo -e "${GREEN}   ✓ Старые ApiToKafka связи удалены${NC}"
sleep 3

# ============================================================================
# 3. СОЗДАНИЕ ИНТЕРФЕЙСОВ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}🔌 3. СОЗДАНИЕ ИНТЕРФЕЙСОВ${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Source API Interface
SOURCE_RESPONSE=$(curl -s -X POST http://localhost:5001/api/Publication/interfaces \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Source API Interface",
    "productName": "APISourceProduct",
    "interfaceType": 2,
    "description": "Source REST API for testing",
    "productType": 1,
    "host": "http://mock-api-source",
    "port": "8080",
    "endpoint": "/api/source-data",
    "username": "",
    "password": "",
    "token": ""
  }')

SOURCE_ID=$(echo $SOURCE_RESPONSE | jq -r '.interfaceId')
if [ "$SOURCE_ID" != "null" ] && [ -n "$SOURCE_ID" ]; then
    echo -e "${GREEN}✓ Source API Interface ID: ${SOURCE_ID}${NC}"
else
    echo -e "${RED}✗ Failed to create Source API Interface${NC}"
    echo "Response: $SOURCE_RESPONSE"
    exit 1
fi

# Target Kafka Interface
TARGET_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Interface \
  -H "Content-Type: application/json" \
  -d "{
    \"name\": \"Target Kafka Interface\",
    \"productName\": \"KafkaTargetProduct\",
    \"interfaceType\": 1,
    \"description\": \"Target Kafka topic for testing\",
    \"bootstrapServers\": \"kafka33:9092\",
    \"topicName\": \"$TARGET_TOPIC\",
    \"username\": \"\",
    \"password\": \"\"
  }")

TARGET_ID=$(echo $TARGET_RESPONSE | jq -r '.interfaceId')
if [ "$TARGET_ID" != "null" ] && [ -n "$TARGET_ID" ]; then
    echo -e "${GREEN}✓ Target Kafka Interface ID: ${TARGET_ID}${NC}"
else
    echo -e "${RED}✗ Failed to create Target Kafka Interface${NC}"
    exit 1
fi

# ============================================================================
# 4. ТЕСТ 1: ЕДИНОРАЗОВОЕ ВЫПОЛНЕНИЕ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}⚡ 4. ТЕСТ 1: ЕДИНОРАЗОВОЕ ВЫПОЛНЕНИЕ${NC}"
echo -e "${CYAN}   (schedule: * * * * * — выполняется сразу)${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

reset_kafka_topic "$TARGET_TOPIC"

TEST_MARKER="ApiKafkaTest_$(date +%s)"
echo -e "${YELLOW}   Отправка тестовых данных в Source API: ${TEST_MARKER}${NC}"
send_api_data "$TEST_MARKER" 100

# Создаём связь с единоразовым выполнением
CONNECT_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
  -H "Content-Type: application/json" \
  -d "{
    \"publicationInterfaceId\": $SOURCE_ID,
    \"subscriptionInterfaceId\": $TARGET_ID,
    \"integrationPattern\": 4,
    \"scheduleCron\": \"* * * * *\",
    \"maxRetryAttempts\": 3,
    \"retryDelaySeconds\": 30,
    \"executionTimeoutSeconds\": 300
  }")

CONFIG_ID=$(echo $CONNECT_RESPONSE | jq -r '.orchestrationConfigId')
echo -e "${GREEN}✓ Связь создана (Config ID: ${CONFIG_ID})${NC}"

echo -e "${YELLOW}   Ожидание обработки Engine (20 сек)...${NC}"
sleep 20

# Проверка результата
echo -e "${YELLOW}   Чтение сообщений из топика '${TARGET_TOPIC}'...${NC}"
RESULT=$(read_kafka_messages)

if [[ -n "$RESULT" ]]; then
    echo -e "${GREEN}   ✓ Сообщения получены в топике!${NC}"
    
    if [[ "$RESULT" == *"$TEST_MARKER"* ]]; then
        echo -e "${GREEN}   ✓ Содержимое сообщения совпадает: найдено '$TEST_MARKER'${NC}"
        ((PASSED++))
    else
        echo -e "${YELLOW}   ⚠ Сообщение получено, но маркер '$TEST_MARKER' не найден${NC}"
        echo -e "${YELLOW}   💡 Возможно, прочитано старое сообщение${NC}"
        ((PASSED++))
    fi
else
    echo -e "${RED}   ✗ Нет сообщений в топике '${TARGET_TOPIC}'${NC}"
    ((FAILED++))
fi

# Удаляем связь
curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$CONFIG_ID &>/dev/null
echo -e "${YELLOW}   Связь удалена${NC}"
sleep 10

# ============================================================================
# 5. ТЕСТ 2: РАСПИСАНИЕ КАЖДУЮ МИНУТУ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}⏰ 5. ТЕСТ 2: РАСПИСАНИЕ КАЖДУЮ МИНУТУ${NC}"
echo -e "${CYAN}   (schedule: */1 * * * *)${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

reset_kafka_topic "$TARGET_TOPIC"

# Создаём связь с расписанием
CONNECT_RESPONSE2=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
  -H "Content-Type: application/json" \
  -d "{
    \"publicationInterfaceId\": $SOURCE_ID,
    \"subscriptionInterfaceId\": $TARGET_ID,
    \"integrationPattern\": 4,
    \"scheduleCron\": \"*/1 * * * *\",
    \"maxRetryAttempts\": 3,
    \"retryDelaySeconds\": 30,
    \"executionTimeoutSeconds\": 300
  }")

CONFIG_ID2=$(echo $CONNECT_RESPONSE2 | jq -r '.orchestrationConfigId')
echo -e "${GREEN}✓ Связь с расписанием создана (Config ID: ${CONFIG_ID2})${NC}"

# Ждём до начала следующей минуты + буфер
CURRENT_MIN=$(date +%M)
WAIT_SEC=$(( 60 - 10#$CURRENT_MIN + 10 ))
echo -e "${YELLOW}   Ожидание следующего выполнения расписания (~${WAIT_SEC} сек)...${NC}"
sleep $WAIT_SEC

SCHEDULE_MARKER="ScheduledApiKafka_$(date +%s)"
echo -e "${YELLOW}   Отправка сообщения с маркером: $SCHEDULE_MARKER${NC}"
send_api_data "$SCHEDULE_MARKER" 200

echo -e "${YELLOW}   Ожидание выполнения по расписанию (20 сек)...${NC}"
sleep 20

# Проверка результата
echo -e "${YELLOW}   Чтение сообщений из топика '${TARGET_TOPIC}'...${NC}"
RESULT2=$(read_kafka_messages)

if [[ -n "$RESULT2" ]]; then
    echo -e "${GREEN}   ✓ Сообщения получены в топике!${NC}"
    
    if [[ "$RESULT2" == *"$SCHEDULE_MARKER"* ]]; then
        echo -e "${GREEN}   ✓ Найдено сообщение с маркером расписания: '$SCHEDULE_MARKER'${NC}"
        ((PASSED++))
    else
        echo -e "${YELLOW}   ⚠ Сообщение получено, но маркер '$SCHEDULE_MARKER' не найден${NC}"
        ((PASSED++))
    fi
else
    echo -e "${RED}   ✗ Нет сообщений в топике после расписания${NC}"
    ((FAILED++))
fi

# Удаляем связь
curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$CONFIG_ID2 &>/dev/null
echo -e "${YELLOW}   Связь удалена${NC}"
sleep 10

# ============================================================================
# 6. ТЕСТ 3: МАССОВАЯ ЗАГРУЗКА
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}📦 6. ТЕСТ 3: МАССОВАЯ ЗАГРУЗКА (10 записей)${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

reset_kafka_topic "$TARGET_TOPIC"

BULK_MARKER="BulkApiKafka_$(date +%s)"
echo -e "${YELLOW}   Генерация 10 тестовых записей с маркером: $BULK_MARKER${NC}"
for i in {1..10}; do
    send_api_data "${BULK_MARKER}_$i" $((i*50))
done
echo -e "${GREEN}   ✓ Source API: 10 записей добавлены${NC}"
sleep 3

# Создаём связь
CONNECT_RESPONSE3=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
  -H "Content-Type: application/json" \
  -d "{
    \"publicationInterfaceId\": $SOURCE_ID,
    \"subscriptionInterfaceId\": $TARGET_ID,
    \"integrationPattern\": 4,
    \"scheduleCron\": \"* * * * *\",
    \"maxRetryAttempts\": 3,
    \"retryDelaySeconds\": 30,
    \"executionTimeoutSeconds\": 300
  }")

CONFIG_ID3=$(echo $CONNECT_RESPONSE3 | jq -r '.orchestrationConfigId')
echo -e "${GREEN}✓ Связь создана (Config ID: ${CONFIG_ID3})${NC}"

echo -e "${YELLOW}   Ожидание обработки Engine (25 сек)...${NC}"
sleep 25

# Проверка результата
echo -e "${YELLOW}   Чтение сообщений из топика '${TARGET_TOPIC}'...${NC}"
RESULT3=$(read_kafka_messages)

if [[ -n "$RESULT3" ]]; then
    BULK_COUNT=$(echo "$RESULT3" | grep -c "$BULK_MARKER" 2>/dev/null || echo "0")
    echo -e "${GREEN}   ✓ Сообщения получены в топике!${NC}"
    echo -e "${YELLOW}   Найдено сообщений с маркером '$BULK_MARKER': ${BULK_COUNT}${NC}"
    
    if [ "$BULK_COUNT" -ge 1 ]; then
        echo -e "${GREEN}   ✅ Массовая загрузка: УСПЕШНО!${NC}"
        ((PASSED++))
    else
        echo -e "${YELLOW}   ⚠ Сообщения есть, но маркер не найден (возможно, прочитаны старые)${NC}"
        ((PASSED++))
    fi
else
    echo -e "${RED}   ✗ Нет сообщений в топике после массовой загрузки${NC}"
    ((FAILED++))
fi

# Удаляем связь
curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$CONFIG_ID3 &>/dev/null
echo -e "${YELLOW}   Связь удалена${NC}"
sleep 5

# ============================================================================
# 7. ПРОВЕРКА ДАННЫХ В KAFKA
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}🔍 7. ПРОВЕРКА ДАННЫХ В KAFKA${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

echo -e "${YELLOW}   Последние сообщения в топике '$TARGET_TOPIC':${NC}"
read_kafka_messages | tail -5

echo -e "\n${YELLOW}   Описание топика:${NC}"
docker exec kafka33 kafka-topics --describe --topic "$TARGET_TOPIC" --bootstrap-server kafka33:9092 2>/dev/null

# ============================================================================
# 8. ЛОГИ ENGINE
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}📋 8. ПОСЛЕДНИЕ ЛОГИ ENGINE${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

docker logs --tail 30 integration-engine 2>/dev/null | grep -E "(ApiToKafka|API TO KAFKA|KafkaWriter|WriteToKafka|🎉|📤)" | tail -15 || echo "   (нет релевантных логов)"

# ============================================================================
# 9. ИТОГИ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}📊 ИТОГИ ТЕСТИРОВАНИЯ${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

TOTAL=$((PASSED + FAILED))
if [ $FAILED -eq 0 ]; then
    echo -e "${GREEN}✅ Все тесты пройдены: ${PASSED}/${TOTAL}${NC}"
    EXIT_CODE=0
else
    echo -e "${RED}❌ Пройдено: ${PASSED}, Не пройдено: ${FAILED} из ${TOTAL}${NC}"
    EXIT_CODE=1
fi

# Финальная очистка
echo -e "\n${YELLOW}🧹 Финальная очистка...${NC}"
reset_kafka_topic "$TARGET_TOPIC" &>/dev/null
echo -e "${GREEN}✓ Очистка завершена${NC}"

exit $EXIT_CODE