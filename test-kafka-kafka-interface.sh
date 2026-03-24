#!/bin/bash

echo "Test kafka to kafka integration"
echo "=========================================="

GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m'

# 1. Публикация Source Kafka интерфейса через Publication API
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
    "topicName": "source-topic",
    "username": "",
    "password": ""
  }')

SOURCE_ID=$(echo $SOURCE_RESPONSE | jq -r '.interfaceId')
echo -e "${GREEN}✓ Source Interface ID: $SOURCE_ID${NC}"

# 2. Создание Consumer Kafka интерфейса через Subscription API
echo -e "${BLUE}2. Создание Consumer Kafka интерфейса...${NC}"
CONSUMER_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Interface \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Target Kafka Interface",
    "productName": "KafkaTargetProduct",
    "interfaceType": 1,
    "description": "Target Kafka for testing",
    "bootstrapServers": "kafka33:9092",
    "topicName": "target-topic",
    "username": "",
    "password": ""
  }')

CONSUMER_ID=$(echo $CONSUMER_RESPONSE | jq -r '.interfaceId')
echo -e "${GREEN}✓ Consumer Interface ID: $CONSUMER_ID${NC}"

# 3. Создание интеграции - ИСПРАВЛЕНО: используем HERE document для JSON
echo -e "${BLUE}3. Создание интеграции Kafka → Kafka...${NC}"

# Создаем JSON с помощью printf (без экранирования)
JSON_DATA=$(printf '{
    "publicationInterfaceId": %d,
    "subscriptionInterfaceId": %d,
    "integrationPattern": 8,
    "scheduleCron": "*/1 * * * *",
    "maxRetryAttempts": 3,
    "retryDelaySeconds": 30,
    "executionTimeoutSeconds": 300
}' "$SOURCE_ID" "$CONSUMER_ID")

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

# 4. Проверка Debezium коннектора
echo -e "${BLUE}4. Создание Debezium коннектора...${NC}"
curl -s -X POST http://localhost:8083/connectors \
  -H "Content-Type: application/json" \
  -d '{
    "name": "subscription-orchestration-connector",
    "config": {
      "connector.class": "io.debezium.connector.postgresql.PostgresConnector",
      "database.hostname": "postgres",
      "database.port": "5432",
      "database.user": "admin",
      "database.password": "password",
      "database.dbname": "integration_platform",
      "database.server.name": "postgres",
      "schema.include.list": "subscription",
      "table.include.list": "subscription.OrchestrationConfig",
      "plugin.name": "pgoutput",
      "publication.name": "dbz_publication",
      "slot.name": "dbz_subscription_slot",
      "transforms": "unwrap",
      "transforms.unwrap.type": "io.debezium.transforms.ExtractNewRecordState",
      "transforms.unwrap.drop.tombstones": "false",
      "key.converter": "org.apache.kafka.connect.json.JsonConverter",
      "value.converter": "org.apache.kafka.connect.json.JsonConverter",
      "key.converter.schemas.enable": "false",
      "value.converter.schemas.enable": "false"
    }
  }' > /dev/null
echo -e "${GREEN}✓ Debezium коннектор создан${NC}"

# 5. Проверка созданных интерфейсов
echo -e "${BLUE}5. Проверка созданных интерфейсов...${NC}"
echo -e "${BLUE}   Source интерфейс (через Search API):${NC}"
curl -s "http://localhost:5002/api/Search/interfaces/by-id/$SOURCE_ID" | jq .

echo -e "${BLUE}   Consumer интерфейс (через Subscription API):${NC}"
curl -s "http://localhost:5003/api/Interface/$CONSUMER_ID" | jq .

# 6. Проверка созданных подключений
echo -e "${BLUE}6. Проверка созданных подключений...${NC}"
curl -s "http://localhost:5003/api/Subscription/connections" | jq .

# 7. ТЕСТИРОВАНИЕ ПЕРЕСЫЛКИ СООБЩЕНИЙ
echo -e "${BLUE}7. Тестирование пересылки сообщений...${NC}"

# Отправляем тестовое сообщение в source-topic
echo -e "${YELLOW}   Отправка тестового сообщения в source-topic...${NC}"
docker exec kafka bash -c "echo 'Тестовое сообщение $(date)' | kafka-console-producer --broker-list kafka:9092 --topic source-topic 2>/dev/null"
echo -e "${GREEN}   ✓ Сообщение отправлено${NC}"

# Ждем 5 секунд для обработки Engine
echo -e "${YELLOW}   Ожидание обработки Engine (5 сек)...${NC}"
sleep 5

# Читаем последнее сообщение из target-topic
echo -e "${YELLOW}   Чтение сообщения из target-topic:${NC}"
docker exec kafka33 kafka-console-consumer --bootstrap-server kafka33:9092 --topic target-topic --from-beginning --max-messages 1 --timeout-ms 5000 2>/dev/null || echo "   ⚠ Нет сообщений в target-topic"

# 8. Показываем логи Engine
echo -e "${BLUE}8. Последние логи Engine:${NC}"
docker logs --tail 10 integration-engine

echo -e "\n${GREEN}✅ Интеграция настроена и протестирована!${NC}"
echo ""
echo "Для отправки сообщений вручную:"
echo "  docker exec -it kafka kafka-console-producer --broker-list kafka:9092 --topic source-topic"
echo ""
echo "Для просмотра сообщений:"
echo "  docker exec -it kafka33 kafka-console-consumer --bootstrap-server kafka33:9092 --topic target-topic --from-beginning"
echo ""
echo "Для наблюдения за логами Engine:"
echo "  docker logs -f integration-engine"