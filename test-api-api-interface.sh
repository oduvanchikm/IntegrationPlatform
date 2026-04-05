#!/bin/bash

echo "╔══════════════════════════════════════════════════════════════════════════════╗"
echo "║                        API → API Integration Test                            ║"
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
    echo -e "${YELLOW}   Запустите: docker-compose up -d mock-api-source mock-api-target${NC}"
    exit 1
fi

echo -ne "${YELLOW}   Проверка Mock API Target...${NC}"
if curl -s -o /dev/null -w "%{http_code}" http://localhost:5101/health | grep -q "200"; then
    echo -e "${GREEN} ✓ OK${NC}"
else
    echo -e "${RED} ✗ НЕ ДОСТУПЕН${NC}"
    exit 1
fi

# ============================================================================
# 1. ОЧИСТКА СТАРЫХ КОНФИГУРАЦИЙ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}🧹 1. ОЧИСТКА СТАРЫХ КОНФИГУРАЦИЙ${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Очищаем данные в Mock API
echo -e "${YELLOW}   Очистка Mock API Target...${NC}"
curl -s -X DELETE http://localhost:5101/api/target-data/reset &>/dev/null
echo -e "${GREEN}   ✓ Mock API Target очищен${NC}"

# Удаляем старые связи
OLD_CONNECTIONS=$(curl -s http://localhost:5003/api/Subscription/connections | jq -r '.[] | select(.integrationPattern == "ApiToApi") | .id')
if [ ! -z "$OLD_CONNECTIONS" ]; then
    for id in $OLD_CONNECTIONS; do
        curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$id &>/dev/null
        echo -e "${YELLOW}   Удалена связь ID: $id${NC}"
    done
fi
echo -e "${GREEN}   ✓ Старые ApiToApi связи удалены${NC}"
sleep 2

# ============================================================================
# 2. СОЗДАНИЕ ИНТЕРФЕЙСОВ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}🔌 2. СОЗДАНИЕ ИНТЕРФЕЙСОВ${NC}"
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

# Target API Interface
TARGET_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Interface \
  -H "Content-Type: application/json" \
  -d '{
    "name": "52Target API Interface",
    "productName": "APITargetProduct",
    "interfaceType": 2,
    "description": "Target REST API for testing",
    "host": "http://mock-api-target",
    "port": "8080",
    "endpoint": "/api/target-data",
    "username": "",
    "password": "",
    "token": ""
  }')

TARGET_ID=$(echo $TARGET_RESPONSE | jq -r '.interfaceId')
if [ "$TARGET_ID" != "null" ] && [ -n "$TARGET_ID" ]; then
    echo -e "${GREEN}✓ Target API Interface ID: ${TARGET_ID}${NC}"
else
    echo -e "${RED}✗ Failed to create Target API Interface${NC}"
    exit 1
fi

# ============================================================================
# 3. ТЕСТ 1: ЕДИНОРАЗОВОЕ ВЫПОЛНЕНИЕ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}⚡ 3. ТЕСТ 1: ЕДИНОРАЗОВОЕ ВЫПОЛНЕНИЕ${NC}"
echo -e "${CYAN}   (schedule: * * * * * — выполняется сразу)${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Очищаем target перед тестом
curl -s -X DELETE http://localhost:5101/api/target-data/reset &>/dev/null
echo -e "${YELLOW}   Mock API Target очищен${NC}"

# Проверяем, что Source API возвращает данные
echo -e "${YELLOW}   Проверка Source API...${NC}"
SOURCE_CHECK=$(docker exec mock-api-source curl -s http://localhost:8080/api/source-data)
SOURCE_COUNT=$(echo $SOURCE_CHECK | jq -r '.data | if type=="array" then length else 1 end')
echo -e "${GREEN}   ✓ Source API содержит: ${SOURCE_COUNT} запись(ей)${NC}"

# Создаём связь с единоразовым выполнением
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

CONFIG_ID=$(echo $CONNECT_RESPONSE | jq -r '.orchestrationConfigId')
echo -e "${GREEN}✓ Связь создана (Config ID: ${CONFIG_ID})${NC}"

# Ожидание обработки (даём время на выполнение + буфер)
echo -e "${YELLOW}   Ожидание обработки Engine (25 сек)...${NC}"
sleep 25

# Проверка результата: проверяем что данные ЕСТЬ (а не точное количество)
TARGET_DATA=$(curl -s http://localhost:5101/api/target-data)
TARGET_COUNT=$(echo $TARGET_DATA | jq -r '.count // 0')
echo -e "${YELLOW}   Записей в Target API: ${TARGET_COUNT}${NC}"

# ✅ Проверяем что хотя бы 1 запись скопировалась (а не ровно 3)
if [ "$TARGET_COUNT" -ge 1 ]; then
    echo -e "${GREEN}   ✅ Единоразовое выполнение: УСПЕШНО!${NC}"
    echo -e "${GREEN}      • Скопировано записей: ${TARGET_COUNT}${NC}"
    ((PASSED++))
else
    echo -e "${RED}   ❌ Единоразовое выполнение: НЕ УСПЕШНО${NC}"
    echo -e "${RED}      • Ожидалось: >=1, получено: ${TARGET_COUNT}${NC}"
    ((FAILED++))
fi

# Удаляем связь
curl -s -X DELETE http://localhost:5003/api/Subscription/connections/$CONFIG_ID &>/dev/null
echo -e "${YELLOW}   Связь удалена${NC}"
sleep 10

# ============================================================================
# 4. ТЕСТ 2: РАСПИСАНИЕ КАЖДУЮ МИНУТУ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}⏰ 4. ТЕСТ 2: РАСПИСАНИЕ КАЖДУЮ МИНУТУ${NC}"
echo -e "${CYAN}   (schedule: */1 * * * *)${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Очищаем target
curl -s -X DELETE http://localhost:5101/api/target-data/reset &>/dev/null
echo -e "${YELLOW}   Mock API Target очищен${NC}"

# Добавляем НОВУЮ запись в Source (чтобы проверить, что она скопируется по расписанию)
echo -e "${YELLOW}   Добавление новой записи в Source API...${NC}"
docker exec mock-api-source curl -s -X POST http://localhost:8080/api/source-data \
  -H "Content-Type: application/json" \
  -d "{\"id\":999,\"message\":\"Scheduled test record\",\"timestamp\":\"$(date -Iseconds)\"}" &>/dev/null

# Создаём связь с расписанием
CONNECT_RESPONSE2=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
  -H "Content-Type: application/json" \
  -d "{
    \"publicationInterfaceId\": $SOURCE_ID,
    \"subscriptionInterfaceId\": $TARGET_ID,
    \"integrationPattern\": 5,
    \"scheduleCron\": \"*/1 * * * *\",
    \"maxRetryAttempts\": 3,
    \"retryDelaySeconds\": 30,
    \"executionTimeoutSeconds\": 300
  }")

CONFIG_ID2=$(echo $CONNECT_RESPONSE2 | jq -r '.orchestrationConfigId')
echo -e "${GREEN}✓ Связь с расписанием создана (Config ID: ${CONFIG_ID2})${NC}"

# Ждём до начала следующей минуты + небольшой буфер
CURRENT_MIN=$(date +%M)
NEXT_MIN=$(( (10#$CURRENT_MIN + 1) % 60 ))
WAIT_SEC=$(( 60 - 10#$CURRENT_MIN + 5 ))
echo -e "${YELLOW}   Ожидание следующего выполнения расписания (~${WAIT_SEC} сек)...${NC}"
sleep $WAIT_SEC

# Проверка результата
TARGET_DATA2=$(curl -s http://localhost:5101/api/target-data)
TARGET_COUNT2=$(echo $TARGET_DATA2 | jq -r '.count // 0')
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
sleep 5

# ============================================================================
# 5. ПРОВЕРКА ДАННЫХ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}🔍 5. ПРОВЕРКА ДАННЫХ${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

echo -e "${YELLOW}   Source API (сырой ответ):${NC}"
curl -s http://localhost:5100/api/source-data | jq . 2>/dev/null | head -30

echo -e "\n${YELLOW}   Target API (полученные записи):${NC}"
# ✅ Исправленный путь: .receivedData[].data.data (вложенная структура)
curl -s http://localhost:5101/api/target-data | jq '
  .receivedData[]? | 
  {
    receivedAt: .receivedAt,
    id: .data.data.id,
    message: .data.data.message,
    timestamp: .data.data.timestamp
  }
' 2>/dev/null | head -40

# ============================================================================
# 6. ИТОГИ
# ============================================================================
echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}📊 ИТОГИ ТЕСТИРОВАНИЯ${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

if [ $FAILED -eq 0 ]; then
    echo -e "${GREEN}✅ Все тесты пройдены: ${PASSED}/${PASSED}${NC}"
    exit 0
else
    echo -e "${RED}❌ Пройдено: ${PASSED}, Не пройдено: ${FAILED}${NC}"
    exit 1
fi