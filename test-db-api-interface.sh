#!/bin/bash

echo "╔══════════════════════════════════════════════════════════════════════════════╗"
echo "║                    Database → API Integration Test                           ║"
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

get_source_count() {
    docker exec postgres-source psql -U admin -d source_db -t -c "SELECT COUNT(*) FROM source.data;" 2>/dev/null | tr -d ' \n'
}

get_source_messages() {
    docker exec postgres-source psql -U admin -d source_db -c "
        SELECT id, payload->>'message' as message, payload->>'value' as value, created_at
        FROM source.data 
        ORDER BY id DESC LIMIT 10;" 2>/dev/null
}

get_target_data() {
    curl -s http://localhost:5101/api/target-data
}

get_target_count() {
    echo "$(get_target_data)" | jq -r '.count // 0'
}

add_source_data() {
    local message=$1
    local value=$2
    docker exec postgres-source psql -U admin -d source_db -c "
        INSERT INTO source.data (payload) 
        VALUES ('{\"message\": \"$message\", \"value\": $value, \"timestamp\": \"$(date -Iseconds)\"}');" &>/dev/null
}

reset_target_api() {
    # Пробуем несколько вариантов эндпоинта для сброса
    curl -s -X DELETE http://localhost:5101/api/target-data/reset &>/dev/null || \
    curl -s -X POST http://localhost:5101/api/target-data -H "Content-Type: application/json" -d '{"reset": true}' &>/dev/null || \
    curl -s -X POST http://localhost:5101/api/target-data/clear &>/dev/null
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

echo -ne "${YELLOW}   Проверка Mock API Target...${NC}"
if curl -s -o /dev/null -w "%{http_code}" http://localhost:5101/health | grep -q "200"; then
    echo -e "${GREEN} ✓ OK${NC}"
else
    echo -e "${RED} ✗ НЕ ДОСТУПЕН${NC}"
    echo -e "${YELLOW}   Запустите: docker-compose up -d mock-api-target${NC}"
    exit 1
fi

# ============================================================================
# 1. ПОДГОТОВКА БАЗЫ ДАННЫХ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}📊 1. ПОДГОТОВКА БАЗЫ ДАННЫХ${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Создаём схему source, если нет
docker exec postgres-source psql -U admin -d source_db -c "CREATE SCHEMA IF NOT EXISTS source;" &>/dev/null

# Очищаем и наполняем source базу
docker exec postgres-source psql -U admin -d source_db -c "TRUNCATE source.data RESTART IDENTITY CASCADE;" &>/dev/null

echo -e "${YELLOW}   Наполнение Source Database тестовыми данными...${NC}"
for i in {1..3}; do
    add_source_data "DB→API Test Record $i" $((i*100))
done

SOURCE_COUNT=$(get_source_count)
echo -e "${GREEN}   ✓ Source Database: ${SOURCE_COUNT} записей${NC}"

# Очищаем Target API
reset_target_api
echo -e "${GREEN}   ✓ Target API очищен${NC}"

# ============================================================================
# 2. ОЧИСТКА СТАРЫХ КОНФИГУРАЦИЙ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}🧹 2. ОЧИСТКА СТАРЫХ КОНФИГУРАЦИЙ${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Удаляем старые DatabaseToApi связи
OLD_CONNECTIONS=$(curl -s http://localhost:5003/api/Subscription/connections | jq -r '.[] | select(.integrationPattern == "DatabaseToApi") | .id' 2>/dev/null)
if [ -n "$OLD_CONNECTIONS" ]; then
    for id in $OLD_CONNECTIONS; do
        curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$id &>/dev/null
        echo -e "${YELLOW}   Удалена связь ID: $id${NC}"
    done
fi
echo -e "${GREEN}   ✓ Старые DatabaseToApi связи удалены${NC}"
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
    "description": "Source PostgreSQL database for batch replication",
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
    echo "Response: $SOURCE_RESPONSE"
    exit 1
fi
echo -e "${GREEN}✓ Source Database Interface ID: ${SOURCE_ID}${NC}"

# Target API Interface
TARGET_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Interface \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Target API Interface",
    "productName": "APITargetProduct",
    "interfaceType": 2,
    "description": "Target REST API for batch replication",
    "host": "http://mock-api-target",
    "port": "8080",
    "endpoint": "/api/target-data",
    "username": "",
    "password": "",
    "token": ""
  }')

TARGET_ID=$(echo $TARGET_RESPONSE | jq -r '.interfaceId // .id // empty')
if [ -z "$TARGET_ID" ] || [ "$TARGET_ID" = "null" ]; then
    echo -e "${RED}✗ Failed to create Target API Interface${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Target API Interface ID: ${TARGET_ID}${NC}"

# ============================================================================
# 4. ТЕСТ 1: ЕДИНОРАЗОВОЕ ВЫПОЛНЕНИЕ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}⚡ 4. ТЕСТ 1: ЕДИНОРАЗОВОЕ ВЫПОЛНЕНИЕ${NC}"
echo -e "${CYAN}   (schedule: * * * * * — выполняется сразу)${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Очищаем target перед тестом
reset_target_api
echo -e "${YELLOW}   Target API очищен${NC}"

# Создаём связь с единоразовым выполнением (интеграция #6 = DatabaseToApi)
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
TARGET_COUNT=$(get_target_count)
echo -e "${YELLOW}   Записей в Target API: ${TARGET_COUNT}${NC}"

if [ "$TARGET_COUNT" -ge 1 ]; then
    echo -e "${GREEN}   ✅ Единоразовое выполнение: УСПЕШНО!${NC}"
    echo -e "${GREEN}      • Передано записей: ${TARGET_COUNT}${NC}"
    ((PASSED++))
else
    echo -e "${RED}   ❌ Единоразовое выполнение: НЕ УСПЕШНО${NC}"
    echo -e "${RED}      • Ожидалось: >=1, получено: ${TARGET_COUNT}${NC}"
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

# Очищаем target
reset_target_api
echo -e "${YELLOW}   Target API очищен${NC}"

# Создаём связь с расписанием
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

CONFIG_ID2=$(echo $CONNECT_RESPONSE2 | jq -r '.orchestrationConfigId // .id // empty')
echo -e "${GREEN}✓ Связь с расписанием создана (Config ID: ${CONFIG_ID2})${NC}"

# Ждём до начала следующей минуты + буфер
CURRENT_MIN=$(date +%M)
WAIT_SEC=$(( 60 - 10#$CURRENT_MIN + 10 ))
echo -e "${YELLOW}   Ожидание следующего выполнения расписания (~${WAIT_SEC} сек)...${NC}"
sleep $WAIT_SEC

# Добавляем новые данные в Source Database после запуска расписания
echo -e "${YELLOW}   Добавление новых записей в Source Database...${NC}"
for i in {1..2}; do
    add_source_data "Scheduled DB Record $i" $((i*200))
done
echo -e "${GREEN}   ✓ Добавлено 2 новые записи${NC}"

# Ждём выполнения по расписанию
echo -e "${YELLOW}   Ожидание выполнения по расписанию (15 сек)...${NC}"
wait_for_engine 15

# Проверка результата
TARGET_COUNT2=$(get_target_count)
echo -e "${YELLOW}   Записей в Target API после расписания: ${TARGET_COUNT2}${NC}"

if [ "$TARGET_COUNT2" -ge 1 ]; then
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

# Очищаем source и target
docker exec postgres-source psql -U admin -d source_db -c "TRUNCATE source.data RESTART IDENTITY CASCADE;" &>/dev/null
reset_target_api

# Генерируем 10 тестовых записей в Source Database
echo -e "${YELLOW}   Генерация 10 тестовых записей в Source Database...${NC}"
for i in {1..10}; do
    add_source_data "Bulk DB→API Record $i" $((i*50))
done
echo -e "${GREEN}   ✓ Source Database: 10 записей добавлены${NC}"

# Создаём связь для массовой загрузки
CONNECT_RESPONSE3=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
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

CONFIG_ID3=$(echo $CONNECT_RESPONSE3 | jq -r '.orchestrationConfigId // .id // empty')
echo -e "${GREEN}✓ Связь создана (Config ID: ${CONFIG_ID3})${NC}"

echo -e "${YELLOW}   Ожидание обработки Engine (20 сек)...${NC}"
wait_for_engine 20

TARGET_COUNT3=$(get_target_count)
echo -e "${YELLOW}   Записей в Target API: ${TARGET_COUNT3}${NC}"

if [ "$TARGET_COUNT3" -ge 1 ]; then
    echo -e "${GREEN}   ✅ Массовая загрузка: УСПЕШНО!${NC}"
    echo -e "${GREEN}      • Передано записей: ${TARGET_COUNT3}${NC}"
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

echo -e "${YELLOW}   Source Database (последние 5 записей):${NC}"
get_source_messages

echo -e "\n${YELLOW}   Target API (полученные записи):${NC}"
# Показываем данные с правильным путём к вложенным полям
get_target_data | jq '
  .receivedData[]? | 
  {
    receivedAt: .receivedAt,
    id: .data.id,
    message: .data.message,
    value: .data.value,
    timestamp: .data.timestamp
  }
' 2>/dev/null | head -40

# ============================================================================
# 8. ДИАГНОСТИКА ПРИ ОШИБКАХ
# ============================================================================
if [ $FAILED -gt 0 ]; then
    echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BLUE}🔧 8. ДИАГНОСТИКА${NC}"
    echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    
    echo -e "${YELLOW}   Проверка доступности Source DB из Engine:${NC}"
    docker exec integration-engine bash -c "echo > /dev/tcp/postgres-source/5432" 2>/dev/null && echo "   ✓ Порт 5432 открыт" || echo "   ✗ Порт 5432 недоступен"
    
    echo -e "${YELLOW}   Проверка доступности Target API из Engine:${NC}"
    docker exec integration-engine curl -s -o /dev/null -w "   HTTP Status: %{http_code}\n" http://mock-api-target:8080/api/target-data
    
    echo -e "${YELLOW}   Последние логи DatabaseToApiHandler:${NC}"
    docker logs --tail 30 integration-engine 2>&1 | grep -E "(DatabaseToApi|DATABASE TO API|ApiWriter|Successfully sent)" || echo "   (нет релевантных записей)"
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
docker exec postgres-source psql -U admin -d source_db -c "TRUNCATE source.data RESTART IDENTITY CASCADE;" &>/dev/null
reset_target_api
echo -e "${GREEN}✓ Очистка завершена${NC}"

exit $EXIT_CODE