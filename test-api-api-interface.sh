#!/bin/bash

echo "Test API to API integration"
echo "=========================================="

GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

# 1. Публикация Source API интерфейса через Publication API
echo -e "${BLUE}1. Публикация Source API интерфейса...${NC}"
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

SOURCE_ID=$(echo $SOURCE_RESPONSE | jq -r '.interfaceId')
echo -e "${GREEN}✓ Source API Interface ID: $SOURCE_ID${NC}"

# 2. Создание Target API интерфейса через Subscription API
echo -e "${BLUE}2. Создание Target API интерфейса...${NC}"
TARGET_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Interface \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Target API Interface",
    "productName": "APITargetProduct",
    "interfaceType": 2,
    "description": "Target API for testing",
    "host": "http://mock-api-target",
    "port": "8080",
    "endpoint": "/api/target-data",
    "username": "",
    "password": "",
    "token": ""
  }')

TARGET_ID=$(echo $TARGET_RESPONSE | jq -r '.interfaceId')
echo -e "${GREEN}✓ Target API Interface ID: $TARGET_ID${NC}"

# 3. Создание интеграции API → API
echo -e "${BLUE}3. Создание интеграции API → API...${NC}"

# Создаем JSON с помощью printf
JSON_DATA=$(printf '{
    "publicationInterfaceId": %d,
    "subscriptionInterfaceId": %d,
    "integrationPattern": 5,
    "scheduleCron": "*/1 * * * *",
    "maxRetryAttempts": 3,
    "retryDelaySeconds": 30,
    "executionTimeoutSeconds": 300
}' "$SOURCE_ID" "$TARGET_ID")

echo "Sending JSON: $JSON_DATA"

CONNECT_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
  -H "Content-Type: application/json" \
  -d "$JSON_DATA")

echo "Connect response: $CONNECT_RESPONSE"

# Проверяем ответ
if echo "$CONNECT_RESPONSE" | grep -q "orchestrationConfigId"; then
    CONFIG_ID=$(echo "$CONNECT_RESPONSE" | jq -r '.orchestrationConfigId')
    echo -e "${GREEN}✓ Orchestration Config ID: $CONFIG_ID${NC}"
else
    echo -e "${RED}✗ Failed to get Orchestration Config ID${NC}"
    echo "Full response: $CONNECT_RESPONSE"
fi

# 4. Запуск заглушек для API (Mock API серверы)
echo -e "${BLUE}4. Проверка Mock API серверов...${NC}"

# Проверяем, запущены ли mock API серверы в Docker
if docker ps | grep -q mock-api-source; then
    echo -e "${GREEN}   ✓ Mock API Source running in Docker on port 5100${NC}"
else
    echo -e "${YELLOW}   ⚠ Mock API Source не запущен в Docker${NC}"
    echo "   Запустите: docker-compose up -d mock-api-source mock-api-target"
fi

if docker ps | grep -q mock-api-target; then
    echo -e "${GREEN}   ✓ Mock API Target running in Docker on port 5101${NC}"
else
    echo -e "${YELLOW}   ⚠ Mock API Target не запущен в Docker${NC}"
fi

# 5. Проверка созданных интерфейсов
echo -e "${BLUE}5. Проверка созданных интерфейсов...${NC}"
echo -e "${BLUE}   Source API интерфейс (через Search API):${NC}"
curl -s "http://localhost:5002/api/Search/interfaces/by-id/$SOURCE_ID" | jq .

echo -e "${BLUE}   Target API интерфейс (через Subscription API):${NC}"
curl -s "http://localhost:5003/api/Interface/$TARGET_ID" | jq .

# 6. Проверка созданных подключений
echo -e "${BLUE}6. Проверка созданных подключений...${NC}"
curl -s "http://localhost:5003/api/Subscription/connections" | jq '.[] | select(.integrationPattern == "ApiToApi")'

# 7. Тестирование отправки данных через API
echo -e "${BLUE}7. Тестирование API-to-API...${NC}"

# Отправляем тестовые данные в source API (через HTTP POST)
echo -e "${YELLOW}   Отправка тестовых данных в source API...${NC}"
curl -s -X POST http://localhost:5100/api/source-data \
  -H "Content-Type: application/json" \
  -d '{
    "id": 1,
    "message": "Test API to API integration",
    "timestamp": "'$(date -Iseconds)'"
  }' || echo "   ⚠ Source API не отвечает"

# Ждем 5 секунд для обработки Engine
echo -e "${YELLOW}   Ожидание обработки Engine (5 сек)...${NC}"
sleep 5

# Проверяем, что данные пришли в target API
echo -e "${YELLOW}   Проверка данных в target API...${NC}"
curl -s http://localhost:5101/api/target-data | jq . || echo "   ⚠ Target API не отвечает"

# 8. Показываем логи Engine
echo -e "${BLUE}8. Последние логи Engine:${NC}"
docker logs --tail 20 integration-engine

echo -e "\n${GREEN}✅ API-to-API интеграция настроена!${NC}"
echo ""
echo "Для запуска mock API серверов:"
echo "  # Source API (порт 5100)"
echo "  cd IntegrationPlatform.MockApi && dotnet run --urls=http://localhost:5100"
echo ""
echo "  # Target API (порт 5101)"
echo "  cd IntegrationPlatform.MockApi && dotnet run --urls=http://localhost:5101"
echo ""
echo "Для отправки тестовых данных:"
echo "  curl -X POST http://localhost:5100/api/source-data -H 'Content-Type: application/json' -d '{\"message\":\"Hello\"}'"
echo ""
echo "Для просмотра полученных данных:"
echo "  curl http://localhost:5101/api/target-data"
echo ""
echo "Для наблюдения за логами Engine:"
echo "  docker logs -f integration-engine"