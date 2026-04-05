#!/bin/bash
 
echo "╔══════════════════════════════════════════════════════════════════════════════╗"
echo "║                     Database → Database Integration Test                     ║"
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
# 1. ПОДГОТОВКА БАЗ ДАННЫХ
# ============================================================================
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}📊 1. ПОДГОТОВКА БАЗ ДАННЫХ${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Проверка доступности баз
echo -ne "${YELLOW}   Проверка source базы...${NC}"
if docker exec postgres-source pg_isready -U admin -d source_db &>/dev/null; then
    echo -e "${GREEN} ✓ OK${NC}"
else
    echo -e "${RED} ✗ НЕ ДОСТУПНА${NC}"
    exit 1
fi

echo -ne "${YELLOW}   Проверка target базы...${NC}"
if docker exec postgres-target pg_isready -U admin -d target_db &>/dev/null; then
    echo -e "${GREEN} ✓ OK${NC}"
else
    echo -e "${RED} ✗ НЕ ДОСТУПНА${NC}"
    exit 1
fi

# Очистка и наполнение source базы
echo -e "${YELLOW}   Очистка source базы...${NC}"
docker exec postgres-source psql -U admin -d source_db -c "TRUNCATE source.data;" &>/dev/null

echo -e "${YELLOW}   Наполнение source базы тестовыми данными...${NC}"
docker exec postgres-source psql -U admin -d source_db -c "
INSERT INTO source.data (payload) VALUES 
    ('{\"message\": \"Record 1\", \"value\": 100, \"type\": \"initial\"}'),
    ('{\"message\": \"Record 2\", \"value\": 200, \"type\": \"initial\"}'),
    ('{\"message\": \"Record 3\", \"value\": 300, \"type\": \"initial\"}');" &>/dev/null

SOURCE_COUNT=$(docker exec postgres-source psql -U admin -d source_db -t -c "SELECT COUNT(*) FROM source.data;" | tr -d ' ')
echo -e "${GREEN}   ✓ Source база: ${SOURCE_COUNT} записей${NC}"

# Очистка target базы
docker exec postgres-target psql -U admin -d target_db -c "TRUNCATE target.data;" &>/dev/null
TARGET_COUNT=$(docker exec postgres-target psql -U admin -d target_db -t -c "SELECT COUNT(*) FROM target.data;" | tr -d ' ')
echo -e "${GREEN}   ✓ Target база: ${TARGET_COUNT} записей (очищена)${NC}"

# ============================================================================
# 2. ОЧИСТКА СТАРЫХ КОНФИГУРАЦИЙ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}🧹 2. ОЧИСТКА СТАРЫХ КОНФИГУРАЦИЙ${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Удаляем старые связи
OLD_CONNECTIONS=$(curl -s http://localhost:5003/api/Subscription/connections | jq -r '.[].id')
if [ ! -z "$OLD_CONNECTIONS" ]; then
    for id in $OLD_CONNECTIONS; do
        curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$id &>/dev/null
        echo -e "${YELLOW}   Удалена связь ID: $id${NC}"
    done
fi
echo -e "${GREEN}   ✓ Старые связи удалены${NC}"
sleep 2

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

SOURCE_ID=$(echo $SOURCE_RESPONSE | jq -r '.interfaceId')
echo -e "${GREEN}✓ Source Database Interface ID: ${SOURCE_ID}${NC}"

# Target Database Interface
TARGET_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Interface \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Target Database Interface",
    "productName": "TargetDBProduct",
    "interfaceType": 0,
    "description": "Target PostgreSQL database for batch replication",
    "host": "postgres-target",
    "port": "5432",
    "username": "admin",
    "password": "password",
    "databaseName": "target_db",
    "scheme": "target"
  }')

TARGET_ID=$(echo $TARGET_RESPONSE | jq -r '.interfaceId')
echo -e "${GREEN}✓ Target Database Interface ID: ${TARGET_ID}${NC}"

# ============================================================================
# 4. ТЕСТ 1: ЕДИНОРАЗОВОЕ ВЫПОЛНЕНИЕ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}⚡ 4. ТЕСТ 1: ЕДИНОРАЗОВОЕ ВЫПОЛНЕНИЕ${NC}"
echo -e "${CYAN}   (schedule: * * * * * — выполняется сразу, без расписания)${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Очищаем target перед тестом
docker exec postgres-target psql -U admin -d target_db -c "TRUNCATE target.data;" &>/dev/null
echo -e "${YELLOW}   Target база очищена${NC}"

# Создаём связь с единоразовым выполнением
CONNECT_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
  -H "Content-Type: application/json" \
  -d "{
    \"publicationInterfaceId\": $SOURCE_ID,
    \"subscriptionInterfaceId\": $TARGET_ID,
    \"integrationPattern\": 0,
    \"scheduleCron\": \"* * * * *\",
    \"maxRetryAttempts\": 3,
    \"retryDelaySeconds\": 30,
    \"executionTimeoutSeconds\": 300
  }")

CONFIG_ID=$(echo $CONNECT_RESPONSE | jq -r '.orchestrationConfigId')
echo -e "${GREEN}✓ Связь создана (Config ID: ${CONFIG_ID})${NC}"

# Ожидание обработки
echo -e "${YELLOW}   Ожидание обработки Engine (25 сек)...${NC}"
sleep 25

# Проверка результата
TARGET_COUNT=$(docker exec postgres-target psql -U admin -d target_db -t -c "SELECT COUNT(*) FROM target.data;" | tr -d ' ')
echo -e "${YELLOW}   Записей в target базе: ${TARGET_COUNT}${NC}"

if [ "$TARGET_COUNT" -eq 3 ]; then
    echo -e "${GREEN}   ✅ Единоразовое выполнение: УСПЕШНО!${NC}"
    ((PASSED++))
else
    echo -e "${RED}   ❌ Единоразовое выполнение: НЕ УСПЕШНО${NC}"
    echo -e "${RED}      • Ожидалось: 3, получено: ${TARGET_COUNT}${NC}"
    ((FAILED++))
fi

# Удаляем связь
curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$CONFIG_ID &>/dev/null
echo -e "${YELLOW}   Связь удалена${NC}"
sleep 10

# ============================================================================
# 5. ТЕСТ 2: РАСПИСАНИЕ КАЖДУЮ МИНУТУ (проверяем только первую копию)
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}⏰ 5. ТЕСТ 2: РАСПИСАНИЕ КАЖДУЮ МИНУТУ${NC}"
echo -e "${CYAN}   (schedule: */1 * * * * — выполняется каждую минуту)${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Очищаем target перед тестом
docker exec postgres-target psql -U admin -d target_db -c "TRUNCATE target.data;" &>/dev/null
echo -e "${YELLOW}   Target база очищена${NC}"
sleep 10

# Создаём связь с расписанием
CONNECT_RESPONSE2=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
  -H "Content-Type: application/json" \
  -d "{
    \"publicationInterfaceId\": $SOURCE_ID,
    \"subscriptionInterfaceId\": $TARGET_ID,
    \"integrationPattern\": 0,
    \"scheduleCron\": \"*/1 * * * *\",
    \"maxRetryAttempts\": 3,
    \"retryDelaySeconds\": 30,
    \"executionTimeoutSeconds\": 300
  }")

sleep 10

CONFIG_ID2=$(echo $CONNECT_RESPONSE2 | jq -r '.orchestrationConfigId')
echo -e "${GREEN}✓ Связь с расписанием создана (Config ID: ${CONFIG_ID2})${NC}"
sleep 10
# Первая копия
echo -e "${YELLOW}   Ожидание первой копии (30 сек)...${NC}"
sleep 30
sleep 10
TARGET_COUNT=$(docker exec postgres-target psql -U admin -d target_db -t -c "SELECT COUNT(*) FROM target.data;" | tr -d ' ')
sleep 10
echo -e "${YELLOW}   Записей в target после первой копии: ${TARGET_COUNT}${NC}"

if [ "$TARGET_COUNT" -eq 3 ]; then
    echo -e "${GREEN}   ✅ Расписание (первая копия): УСПЕШНО!${NC}"
    ((PASSED++))
else
    echo -e "${RED}   ❌ Расписание (первая копия): НЕ УСПЕШНО${NC}"
    ((FAILED++))
fi

# ============================================================================
# 7. ТЕСТ 4: МАССОВАЯ ЗАГРУЗКА (100 записей)
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}📦 7. ТЕСТ 4: МАССОВАЯ ЗАГРУЗКА (100 записей)${NC}"
echo -e "${CYAN}   Проверка работы пагинации и COPY для больших объёмов данных${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Удаляем старую связь с расписанием
curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$CONFIG_ID2 &>/dev/null
sleep 3

# Очищаем source и target
docker exec postgres-source psql -U admin -d source_db -c "TRUNCATE source.data;" &>/dev/null
docker exec postgres-target psql -U admin -d target_db -c "TRUNCATE target.data;" &>/dev/null

# Генерируем 100 тестовых записей
echo -e "${YELLOW}   Генерация 100 тестовых записей...${NC}"
for i in {1..100}; do
    docker exec postgres-source psql -U admin -d source_db -c "
        INSERT INTO source.data (payload) VALUES 
        ('{\"message\": \"Bulk record $i\", \"value\": $i, \"type\": \"bulk\"}');" &>/dev/null
done

SOURCE_COUNT=$(docker exec postgres-source psql -U admin -d source_db -t -c "SELECT COUNT(*) FROM source.data;" | tr -d ' ')
echo -e "${GREEN}   ✓ Source база: ${SOURCE_COUNT} записей${NC}"

# Создаём новую связь для массовой загрузки
CONNECT_RESPONSE3=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
  -H "Content-Type: application/json" \
  -d "{
    \"publicationInterfaceId\": $SOURCE_ID,
    \"subscriptionInterfaceId\": $TARGET_ID,
    \"integrationPattern\": 0,
    \"scheduleCron\": \"* * * * *\",
    \"maxRetryAttempts\": 3,
    \"retryDelaySeconds\": 30,
    \"executionTimeoutSeconds\": 300
  }")

CONFIG_ID3=$(echo $CONNECT_RESPONSE3 | jq -r '.orchestrationConfigId')
echo -e "${GREEN}✓ Связь создана (Config ID: ${CONFIG_ID3})${NC}"

echo -e "${YELLOW}   Ожидание обработки Engine (20 сек)...${NC}"
sleep 20

TARGET_COUNT=$(docker exec postgres-target psql -U admin -d target_db -t -c "SELECT COUNT(*) FROM target.data;" | tr -d ' ')
echo -e "${YELLOW}   Записей в target базе: ${TARGET_COUNT}${NC}"

if [ "$TARGET_COUNT" -eq 100 ]; then
    echo -e "${GREEN}   ✅ Массовая загрузка: УСПЕШНО!${NC}"
    ((PASSED++))
else
    echo -e "${RED}   ❌ Массовая загрузка: НЕ УСПЕШНО${NC}"
    echo -e "${RED}      • Ожидалось: 100, получено: ${TARGET_COUNT}${NC}"
    ((FAILED++))
fi

# ============================================================================
# 8. ПРОВЕРКА ДАННЫХ В БАЗАХ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}🔍 8. ПРОВЕРКА ДАННЫХ В БАЗАХ${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

echo -e "${YELLOW}   Source база (последние 5 записей):${NC}"
docker exec postgres-source psql -U admin -d source_db -c "SELECT id, payload->>'message' as message, payload->>'type' as type FROM source.data ORDER BY id DESC LIMIT 5;" 2>/dev/null

echo -e "\n${YELLOW}   Target база (последние 5 записей):${NC}"
docker exec postgres-target psql -U admin -d target_db -c "SELECT id, payload->>'message' as message, payload->>'type' as type FROM target.data ORDER BY id DESC LIMIT 5;" 2>/dev/null


echo -e "\n${GREEN}✅ Всего успешно: ${PASSED} из 4 тестов${NC}"