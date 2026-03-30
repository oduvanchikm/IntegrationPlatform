#!/bin/bash

echo "Test API to Database integration"
echo "=========================================="

GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

# ==========================================
# Функции для ожидания и проверки
# ==========================================
wait_for_engine_processing() {
    local seconds=$1
    echo -e "${YELLOW}   Ожидание обработки Engine (${seconds} сек)...${NC}"
    for i in $(seq 1 $seconds); do
        echo -ne "\r   Осталось $((seconds-i)) секунд..."
        sleep 1
    done
    echo ""
}

get_target_count() {
    docker exec postgres-target psql -U admin -d target_db -t -c "SELECT COUNT(*) FROM target.data;" 2>/dev/null | tr -d ' '
}

get_target_messages() {
    # Извлекаем message из вложенного объекта data
    docker exec postgres-target psql -U admin -d target_db -c "SELECT id, payload->'data'->>'message' as message, payload->'data'->>'value' as value, created_at FROM target.data ORDER BY id;" 2>/dev/null
}

get_target_full() {
    # Показываем полный JSON для отладки
    docker exec postgres-target psql -U admin -d target_db -c "SELECT id, payload, created_at FROM target.data ORDER BY id;" 2>/dev/null
}

send_test_data() {
    local message=$1
    local value=$2
    curl -s -X POST http://localhost:5100/api/source-data \
        -H "Content-Type: application/json" \
        -d "{\"message\": \"$message\", \"value\": $value, \"timestamp\": \"$(date -Iseconds)\"}"
}

# ==========================================
# 1. Подготовка базы данных
# ==========================================
echo -e "${BLUE}1. Подготовка базы данных...${NC}"

# Создаем схему target, если не существует
docker exec postgres-target psql -U admin -d target_db -c "CREATE SCHEMA IF NOT EXISTS target;" 2>/dev/null

# Очищаем target базу
docker exec postgres-target psql -U admin -d target_db -c "TRUNCATE target.data RESTART IDENTITY CASCADE;" 2>/dev/null

echo -e "${GREEN}   ✓ Target база очищена${NC}"
wait_for_engine_processing 5

# ==========================================
# 2. Удаляем старые интерфейсы и связи
# ==========================================
echo -e "${BLUE}2. Очистка старых данных...${NC}"

# Удаляем существующие связи
for id in $(curl -s http://localhost:5003/api/Subscription/connections 2>/dev/null | jq -r '.[].id // empty' 2>/dev/null); do
    curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$id 2>/dev/null
    echo "   Удалена связь ID: $id"
done

# Удаляем старые интерфейсы в subscription
for id in $(curl -s http://localhost:5003/api/Interface 2>/dev/null | jq -r '.[].id // empty' 2>/dev/null); do
    curl -s -X DELETE http://localhost:5003/api/Interface/$id 2>/dev/null
    echo "   Удален subscription интерфейс ID: $id"
done

# Удаляем старые интерфейсы в publication
for id in $(curl -s http://localhost:5001/api/Publication/interfaces 2>/dev/null | jq -r '.[].id // empty' 2>/dev/null); do
    curl -s -X DELETE http://localhost:5001/api/Publication/interfaces/$id 2>/dev/null
    echo "   Удален publication интерфейс ID: $id"
done

echo -e "${GREEN}   ✓ Старые данные очищены${NC}"
wait_for_engine_processing 5

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

SOURCE_ID=$(echo $SOURCE_RESPONSE | jq -r '.interfaceId // .id // empty')
if [ -z "$SOURCE_ID" ] || [ "$SOURCE_ID" = "null" ]; then
    echo -e "${RED}   ✗ Ошибка создания Source API интерфейса${NC}"
    echo "Response: $SOURCE_RESPONSE"
    exit 1
fi
echo -e "${GREEN}✓ Source API ID: $SOURCE_ID${NC}"
wait_for_engine_processing 3

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
  
TARGET_ID=$(echo $TARGET_RESPONSE | jq -r '.interfaceId // .id // empty')
if [ -z "$TARGET_ID" ] || [ "$TARGET_ID" = "null" ]; then
    echo -e "${RED}   ✗ Ошибка создания Target Database интерфейса${NC}"
    echo "Response: $TARGET_RESPONSE"
    exit 1
fi
echo -e "${GREEN}✓ Target Database ID: $TARGET_ID${NC}"
wait_for_engine_processing 3

# ==========================================
# 4. Тест 1: Единоразовое выполнение
# ==========================================
echo -e "${BLUE}4. Тест 1: Единоразовое выполнение (schedule: * * * * *)${NC}"

# Очищаем target базу
docker exec postgres-target psql -U admin -d target_db -c "TRUNCATE target.data RESTART IDENTITY CASCADE;" 2>/dev/null
wait_for_engine_processing 5

# Отправляем тестовые данные ДО создания связи
echo -e "${YELLOW}   Отправка тестовых данных в source API...${NC}"
send_test_data "API to Database test" 100
wait_for_engine_processing 2

# Создаем связь
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

CONFIG_ID=$(echo $CONNECT_RESPONSE | jq -r '.orchestrationConfigId // .id // empty')
if [ -z "$CONFIG_ID" ] || [ "$CONFIG_ID" = "null" ]; then
    echo -e "${RED}   ✗ Ошибка создания связи${NC}"
    echo "Response: $CONNECT_RESPONSE"
    exit 1
fi
echo -e "${GREEN}✓ Config ID: $CONFIG_ID${NC}"

# Отправляем еще одно тестовое данные ПОСЛЕ создания связи
wait_for_engine_processing 5
send_test_data "Second test message" 200
wait_for_engine_processing 5

# Ждем выполнения
wait_for_engine_processing 15

TARGET_COUNT=$(get_target_count)
echo -e "${YELLOW}   Записей в target базе: $TARGET_COUNT${NC}"

if [ "$TARGET_COUNT" -ge 1 ]; then
    echo -e "${GREEN}   ✓ Единоразовое выполнение работает${NC}"
    get_target_messages
else
    echo -e "${RED}   ✗ Ожидалось >= 1, получено $TARGET_COUNT${NC}"
fi
wait_for_engine_processing 5

# Удаляем связь
curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$CONFIG_ID 2>/dev/null
echo -e "${GREEN}   ✓ Связь удалена${NC}"
wait_for_engine_processing 10

# ==========================================
# 5. Тест 2: Расписание каждую минуту
# ==========================================
echo -e "${BLUE}5. Тест 2: Расписание каждую минуту (*/1 * * * *)${NC}"

# Очищаем target базу
docker exec postgres-target psql -U admin -d target_db -c "TRUNCATE target.data RESTART IDENTITY CASCADE;" 2>/dev/null
wait_for_engine_processing 5

# Создаем связь с расписанием
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

CONFIG_ID2=$(echo $CONNECT_RESPONSE2 | jq -r '.orchestrationConfigId // .id // empty')
if [ -z "$CONFIG_ID2" ] || [ "$CONFIG_ID2" = "null" ]; then
    echo -e "${RED}   ✗ Ошибка создания связи${NC}"
    echo "Response: $CONNECT_RESPONSE2"
    exit 1
fi
echo -e "${GREEN}✓ Config ID: $CONFIG_ID2${NC}"

# Ждем первого срабатывания расписания
echo -e "${YELLOW}   Ожидание первого срабатывания расписания (70 сек)...${NC}"
wait_for_engine_processing 70
sleep 15

# Отправляем первое сообщение
echo -e "${YELLOW}   Отправка первого сообщения...${NC}"
send_test_data "First message" 100
wait_for_engine_processing 5
sleep 15

# Отправляем новые данные в течение минуты
echo -e "${YELLOW}   Отправка сообщений в течение минуты...${NC}"
for i in {2..4}; do
    send_test_data "Message $i" $((i*100))
    echo -e "${GREEN}   Сообщение $i отправлено${NC}"
    sleep 15
done
sleep 15

# Ждем следующего срабатывания расписания
echo -e "${YELLOW}   Ожидание следующего срабатывания расписания (60 сек)...${NC}"
wait_for_engine_processing 70
sleep 15

TARGET_COUNT=$(get_target_count)
echo -e "${YELLOW}   Записей в target после теста: $TARGET_COUNT${NC}"

if [ "$TARGET_COUNT" -eq 4 ]; then
    echo -e "${GREEN}   ✓ Расписание работает! Все 4 сообщения сохранены${NC}"
    get_target_messages
elif [ "$TARGET_COUNT" -gt 0 ]; then
    echo -e "${YELLOW}   ⚠ Частичный успех: сохранено $TARGET_COUNT из 4 сообщений${NC}"
    get_target_messages
else
    echo -e "${RED}   ✗ Не сохранено ни одного сообщения${NC}"
fi

# ==========================================
# 6. Просмотр логов Engine
# ==========================================
echo -e "${BLUE}6. Последние логи Engine:${NC}"
echo -e "${YELLOW}=== API TO DATABASE HANDLER LOGS ===${NC}"
docker logs --tail 100 integration-engine 2>&1 | grep -E "API TO DATABASE|Starting transfer|Successfully saved|Error during API" | tail -20

echo -e "\n${YELLOW}=== DATABASE WRITER LOGS ===${NC}"
docker logs --tail 50 integration-engine 2>&1 | grep "DatabaseWriter" | tail -10

echo -e "\n${GREEN}✅ Тест API → Database завершен!${NC}"

# ==========================================
# 7. Очистка
# ==========================================
echo -e "${BLUE}7. Очистка тестовых данных...${NC}"
curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$CONFIG_ID2 2>/dev/null
echo -e "${GREEN}   ✓ Тестовые данные очищены${NC}"