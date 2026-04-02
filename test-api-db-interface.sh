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
}

read_kafka_messages() {
    docker exec kafka33 kafka-console-consumer \
        --bootstrap-server kafka33:9092 \
        --topic "$TARGET_TOPIC" \
        --from-beginning \
        --max-messages 200 \
        --timeout-ms 10000 2>/dev/null
}

send_api_data() {
    local message=$1
    local value=${2:-0}
    local timestamp=$(date -Iseconds)
    
    # ✅ КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ: Отправляем через Docker network (как видит Engine)
    # Используем curl внутри контейнера mock-api-source
    docker exec mock-api-source curl -s -X POST http://localhost:8080/api/source-data \
        -H "Content-Type: application/json" \
        -d "{\"id\":$RANDOM,\"message\":\"$message\",\"value\":$value,\"timestamp\":\"$timestamp\"}" &>/dev/null
    
    # Даем время на обработку
    sleep 1
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

# Удаляем старые ApiToKafka связи
OLD_CONNECTIONS=$(curl -s http://localhost:5003/api/Subscription/connections | jq -r '.[] | select(.integrationPattern == "ApiToKafka") | .id' 2>/dev/null)
if [ -n "$OLD_CONNECTIONS" ]; then
    for id in $OLD_CONNECTIONS; do
        curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$id &>/dev/null
        echo -e "${YELLOW}   Удалена связь ID: $id${NC}"
    done
fi
echo -e "${GREEN}   ✓ Старые ApiToKafka связи удалены${NC}"
wait_for_engine 3

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
    "description": "Source REST API for batch replication to Kafka",
    "productType": 1,
    "host": "http://mock-api-source",
    "port": "8080",
    "endpoint": "/api/source-data",
    "username": "",
    "password": "",
    "token": ""
  }')

SOURCE_ID=$(echo $SOURCE_RESPONSE | jq -r '.interfaceId // .id // empty')
if [ -z "$SOURCE_ID" ] || [ "$SOURCE_ID" = "null" ]; then
    echo -e "${RED}✗ Failed to create Source API Interface${NC}"
    echo "Response: $SOURCE_RESPONSE"
    exit 1
fi
echo -e "${GREEN}✓ Source API Interface ID: ${SOURCE_ID}${NC}"

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

# Добавляем тестовые данные в Source API
echo -e "${YELLOW}   Наполнение Source API тестовыми данными...${NC}"
for i in {1..3}; do
    send_api_data "API→Kafka Test Record $i" $((i*100))
done
echo -e "${GREEN}   ✓ Source API: 3 тестовые записи добавлены${NC}"

# Создаём связь с единоразовым выполнением (интеграция #4 = ApiToKafka)
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

CONFIG_ID=$(echo $CONNECT_RESPONSE | jq -r '.orchestrationConfigId // .id // empty')
if [ -z "$CONFIG_ID" ] || [ "$CONFIG_ID" = "null" ]; then
    echo -e "${RED}✗ Failed to create connection${NC}"
    echo "Response: $CONNECT_RESPONSE"
    exit 1
fi
echo -e "${GREEN}✓ Связь создана (Config ID: ${CONFIG_ID})${NC}"

# Ожидание обработки
echo -e "${YELLOW}   Ожидание обработки Engine (25 сек)...${NC}"
wait_for_engine 25

# Проверка результата
echo -e "${YELLOW}   Чтение сообщений из топика '${TARGET_TOPIC}'...${NC}"
RESULT=$(read_kafka_messages)
MESSAGE_COUNT=$(echo "$RESULT" | grep -c "API→Kafka Test Record" 2>/dev/null || echo "0")

if [ "$MESSAGE_COUNT" -ge 1 ]; then
    echo -e "${GREEN}   ✅ Единоразовое выполнение: УСПЕШНО!${NC}"
    echo -e "${GREEN}      • Получено сообщений: ${MESSAGE_COUNT}${NC}"
    ((PASSED++))
else
    echo -e "${RED}   ❌ Единоразовое выполнение: НЕ УСПЕШНО${NC}"
    ((FAILED++))
fi

# Удаляем связь
curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$CONFIG_ID &>/dev/null
echo -e "${YELLOW}   Связь удалена${NC}"
wait_for_engine 10

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

CONFIG_ID2=$(echo $CONNECT_RESPONSE2 | jq -r '.orchestrationConfigId // .id // empty')
echo -e "${GREEN}✓ Связь с расписанием создана (Config ID: ${CONFIG_ID2})${NC}"

# Ждём до начала следующей минуты + буфер
CURRENT_MIN=$(date +%M)
WAIT_SEC=$(( 60 - 10#$CURRENT_MIN + 10 ))
echo -e "${YELLOW}   Ожидание следующего выполнения расписания (~${WAIT_SEC} сек)...${NC}"
sleep $WAIT_SEC

# Добавляем новые данные в Source API после запуска расписания
echo -e "${YELLOW}   Добавление новых записей в Source API...${NC}"
for i in {1..2}; do
    send_api_data "Scheduled Kafka Record $i" $((i*200))
done
echo -e "${GREEN}   ✓ Добавлено 2 новые записи${NC}"

# Ждём выполнения по расписанию
echo -e "${YELLOW}   Ожидание выполнения по расписанию (15 сек)...${NC}"
wait_for_engine 15

# Проверка результата
echo -e "${YELLOW}   Чтение сообщений из топика '${TARGET_TOPIC}'...${NC}"
RESULT2=$(read_kafka_messages)
MESSAGE_COUNT2=$(echo "$RESULT2" | grep -c "Scheduled Kafka Record" 2>/dev/null || echo "0")

if [ "$MESSAGE_COUNT2" -ge 1 ]; then
    echo -e "${GREEN}   ✅ Расписание: УСПЕШНО!${NC}"
    ((PASSED++))
else
    echo -e "${RED}   ❌ Расписание: НЕ УСПЕШНО${NC}"
    ((FAILED++))
fi

# Удаляем связь
curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$CONFIG_ID2 &>/dev/null
echo -e "${YELLOW}   Связь удалена${NC}"
wait_for_engine 5

# ============================================================================
# 6. ТЕСТ 3: МАССОВАЯ ЗАГРУЗКА (10 записей)
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}📦 6. ТЕСТ 3: МАССОВАЯ ЗАГРУЗКА (10 записей)${NC}"
echo -e "${CYAN}   Проверка работы с большим объёмом данных${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

reset_kafka_topic "$TARGET_TOPIC"

# Генерируем 10 тестовых записей в Source API
echo -e "${YELLOW}   Генерация 10 тестовых записей в Source API...${NC}"
for i in {1..10}; do
    send_api_data "Bulk API→Kafka Record $i" $((i*50))
done
echo -e "${GREEN}   ✓ Source API: 10 записей добавлены${NC}"

# Создаём связь для массовой загрузки
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

CONFIG_ID3=$(echo $CONNECT_RESPONSE3 | jq -r '.orchestrationConfigId // .id // empty')
echo -e "${GREEN}✓ Связь создана (Config ID: ${CONFIG_ID3})${NC}"

echo -e "${YELLOW}   Ожидание обработки Engine (20 сек)...${NC}"
wait_for_engine 20

# Проверка результата
echo -e "${YELLOW}   Чтение сообщений из топика '${TARGET_TOPIC}'...${NC}"
RESULT3=$(read_kafka_messages)
MESSAGE_COUNT3=$(echo "$RESULT3" | grep -c "Bulk API→Kafka Record" 2>/dev/null || echo "0")

if [ "$MESSAGE_COUNT3" -ge 1 ]; then
    echo -e "${GREEN}   ✅ Массовая загрузка: УСПЕШНО!${NC}"
    echo -e "${GREEN}      • Получено сообщений: ${MESSAGE_COUNT3}${NC}"
    ((PASSED++))
else
    echo -e "${RED}   ❌ Массовая загрузка: НЕ УСПЕШНО${NC}"
    ((FAILED++))
fi

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
# 8. ДИАГНОСТИКА ПРИ ОШИБКАХ
# ============================================================================
if [ $FAILED -gt 0 ]; then
    echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BLUE}🔧 8. ДИАГНОСТИКА${NC}"
    echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    
    echo -e "${YELLOW}   Проверка доступности Source API из Engine:${NC}"
    docker exec integration-engine curl -s -o /dev/null -w "   HTTP Status: %{http_code}\n" http://mock-api-source:8080/api/source-data
    
    echo -e "${YELLOW}   Последние логи ApiToKafkaHandler:${NC}"
    docker logs --tail 30 integration-engine 2>&1 | grep -E "(ApiToKafka|API TO KAFKA|KafkaWriter|Successfully sent)" | tail -10 || echo "   (нет релевантных записей)"
fi

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
curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$CONFIG_ID3 &>/dev/null
reset_kafka_topic "$TARGET_TOPIC" &>/dev/null
echo -e "${GREEN}✓ Очистка завершена${NC}"

exit $EXIT_CODE