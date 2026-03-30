#!/bin/bash

echo "Test API to Kafka integration"
echo "=========================================="

GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

TARGET_TOPIC="target-topic-api-kafka-interface"

echo -e "${BLUE}0. Создание топика target-topic-api-kafka-interface...${NC}"
docker exec kafka33 kafka-topics --create \
  --topic "$TARGET_TOPIC" \
  --bootstrap-server kafka33:9092 \
  --partitions 1 \
  --replication-factor 1

# 1. Публикация Source API интерфейса (источник)
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

# 2. Создание Target Kafka интерфейса (получатель)
echo -e "${BLUE}2. Создание Target Kafka интерфейса...${NC}"
TARGET_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Interface \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Target Kafka Interface",
    "productName": "KafkaTargetProduct",
    "interfaceType": 1,
    "description": "Target Kafka for testing",
    "bootstrapServers": "kafka33:9092",
    "topicName": "target-topic-api-kafka-interface",
    "username": "",
    "password": ""
  }')

TARGET_ID=$(echo $TARGET_RESPONSE | jq -r '.interfaceId')
echo -e "${GREEN}✓ Target Kafka Interface ID: $TARGET_ID${NC}"

# 3. Создание интеграции API → Kafka
echo -e "${BLUE}3. Создание интеграции API → Kafka...${NC}"

JSON_DATA=$(printf '{
    "publicationInterfaceId": %d,
    "subscriptionInterfaceId": %d,
    "integrationPattern": 4,
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

if echo "$CONNECT_RESPONSE" | grep -q "orchestrationConfigId"; then
    CONFIG_ID=$(echo "$CONNECT_RESPONSE" | jq -r '.orchestrationConfigId')
    echo -e "${GREEN}✓ Orchestration Config ID: $CONFIG_ID${NC}"
else
    echo -e "${RED}✗ Failed to get Orchestration Config ID${NC}"
    echo "Full response: $CONNECT_RESPONSE"
fi

# 4. Проверка Mock API серверов
echo -e "${BLUE}4. Проверка Mock API серверов...${NC}"

if docker ps | grep -q mock-api-source; then
    echo -e "${GREEN}   ✓ Mock API Source running in Docker on port 5100${NC}"
else
    echo -e "${YELLOW}   ⚠ Mock API Source не запущен в Docker${NC}"
    echo "   Запустите: docker-compose up -d mock-api-source"
fi

# 5. Проверка созданных интерфейсов
echo -e "${BLUE}5. Проверка созданных интерфейсов...${NC}"
echo -e "${BLUE}   Source API интерфейс (через Search API):${NC}"
curl -s "http://localhost:5002/api/Search/interfaces/by-id/$SOURCE_ID" | jq .

echo -e "${BLUE}   Target Kafka интерфейс (через Subscription API):${NC}"
curl -s "http://localhost:5003/api/Interface/$TARGET_ID" | jq .

# 6. Проверка созданных подключений
echo -e "${BLUE}6. Проверка созданных подключений...${NC}"
curl -s "http://localhost:5003/api/Subscription/connections" | jq '.[] | select(.integrationPattern == "ApiToKafka")'

# 7. Тестирование пересылки сообщений
echo -e "${BLUE}7. Тестирование API → Kafka...${NC}"

echo -e "${YELLOW}   Отправка тестовых данных в source API...${NC}"
curl -s -X POST http://localhost:5100/api/source-data \
  -H "Content-Type: application/json" \
  -d '{
    "id": 1,
    "message": "Test API to Kafka '$(date -Iseconds)' integration",
    "timestamp": "'$(date -Iseconds)'"
  }' || echo "   ⚠ Source API не отвечает"

echo -e "${YELLOW}   Ожидание обработки Engine (5 сек)...${NC}"
sleep 5

echo -e "${YELLOW}   Проверка сообщений в target-topic-api-kafka-interface...${NC}"
docker exec kafka33 kafka-console-consumer --bootstrap-server kafka33:9092 --topic target-topic-api-kafka-interface --max-messages 1 --timeout-ms 20000 2>/dev/null || echo "   ⚠ Нет сообщений в target-topic-api-kafka-interface"

# 8. Показываем логи Engine
echo -e "${BLUE}8. Последние логи Engine:${NC}"
docker logs --tail 50 integration-engine | grep -E "API TO KAFKA|ApiToKafka"

echo -e "\n${GREEN}✅ API-to-Kafka интеграция настроена!${NC}"
echo ""
echo "Для отправки тестовых данных вручную:"
echo "  curl -X POST http://localhost:5100/api/source-data -H 'Content-Type: application/json' -d '{\"message\":\"Hello\"}'"
echo ""
echo "Для просмотра сообщений в Kafka:"
# shellcheck disable=SC2027
echo "  docker exec -it kafka33 kafka-console-consumer --bootstrap-server kafka33:9092 --topic "$TARGET_TOPIC" --max-messages 1 --timeout-ms 5000"
echo ""
echo "Для наблюдения за логами Engine:"
echo "  docker logs -f integration-engine"