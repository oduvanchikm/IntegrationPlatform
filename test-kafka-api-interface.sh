#!/bin/bash

echo "Test Kafka to API integration"
echo "=========================================="

GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

SOURCE_TOPIC="source-topic-kafka-api-interface"

echo -e "${BLUE}0. Создание топика source-topic-kafka-api-interface...${NC}"
docker exec kafka2 kafka-topics --create \
  --topic "$SOURCE_TOPIC" \
  --bootstrap-server kafka2:9092 \
  --partitions 1 \
  --replication-factor 1

# 1. Публикация Source Kafka интерфейса (источник)
echo -e "${BLUE}1. Публикация Source Kafka интерфейса...${NC}"
SOURCE_RESPONSE=$(curl -s -X POST http://localhost:5001/api/Publication/interfaces \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Source Kafka Interface",
    "productName": "KafkaSourceProduct",
    "interfaceType": 1,
    "description": "Source Kafka for testing",
    "productType": 1,
    "bootstrapServers": "kafka2:9092",
    "topicName": "source-topic-kafka-api-interface",
    "username": "",
    "password": ""
  }')

SOURCE_ID=$(echo $SOURCE_RESPONSE | jq -r '.interfaceId')
echo -e "${GREEN}✓ Source Kafka Interface ID: $SOURCE_ID${NC}"

# 2. Создание Target API интерфейса (получатель)
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

# 3. Создание интеграции Kafka → API
echo -e "${BLUE}3. Создание интеграции Kafka → API...${NC}"

JSON_DATA=$(printf '{
    "publicationInterfaceId": %d,
    "subscriptionInterfaceId": %d,
    "integrationPattern": 7,
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

if docker ps | grep -q mock-api-target; then
    echo -e "${GREEN}   ✓ Mock API Target running in Docker on port 5101${NC}"
else
    echo -e "${YELLOW}   ⚠ Mock API Target не запущен в Docker${NC}"
    echo "   Запустите: docker-compose up -d mock-api-target"
fi

# 5. Проверка созданных интерфейсов
echo -e "${BLUE}5. Проверка созданных интерфейсов...${NC}"
echo -e "${BLUE}   Source Kafka интерфейс (через Search API):${NC}"
curl -s "http://localhost:5002/api/Search/interfaces/by-id/$SOURCE_ID" | jq .

echo -e "${BLUE}   Target API интерфейс (через Subscription API):${NC}"
curl -s "http://localhost:5003/api/Interface/$TARGET_ID" | jq .

# 6. Проверка созданных подключений
echo -e "${BLUE}6. Проверка созданных подключений...${NC}"
curl -s "http://localhost:5003/api/Subscription/connections" | jq '.[] | select(.integrationPattern == "KafkaToApi")'

# 7. Тестирование пересылки сообщений
echo -e "${BLUE}7. Тестирование Kafka → API...${NC}"

# shellcheck disable=SC2027
echo -e "${YELLOW}   Отправка тестового сообщения в "$SOURCE_TOPIC"...${NC}"
echo "Test Kafka to Api integration! $(date)" | docker exec -i kafka2 kafka-console-producer --broker-list kafka2:9092 --topic "$SOURCE_TOPIC"
echo -e "${GREEN}   ✓ Сообщение отправлено в Kafka${NC}"

echo -e "${YELLOW}   Ожидание обработки Engine (5 сек)...${NC}"
sleep 5

echo -e "${YELLOW}   Проверка данных в target API...${NC}"
curl -s http://localhost:5101/api/target-data | jq . || echo "   ⚠ Target API не отвечает"

# 8. Показываем логи Engine
echo -e "${BLUE}8. Последние логи Engine:${NC}"
docker logs --tail 50 integration-engine | grep -E "KAFKA TO API|KafkaToApi"

echo -e "\n${GREEN}✅ Kafka-to-API интеграция настроена!${NC}"
echo ""
echo "Для отправки сообщений вручную:"
# shellcheck disable=SC2027
echo "  echo \"Test message\" | docker exec -i kafka2 kafka-console-producer --broker-list kafka2:9092 --topic "$SOURCE_TOPIC""
echo ""
echo "Для просмотра полученных данных:"
echo "  curl http://localhost:5101/api/target-data"
echo ""
echo "Для наблюдения за логами Engine:"
echo "  docker logs -f integration-engine"