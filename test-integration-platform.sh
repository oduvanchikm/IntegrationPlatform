#!/bin/bash

echo "Test kafka to kafka integration"
echo "=========================================="

GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m'

# 1. Публикация Source Kafka интерфейса
echo -e "${BLUE}1. Публикация Source Kafka интерфейса...${NC}"
SOURCE_RESPONSE=$(curl -s -X POST http://localhost:5001/api/Publication/interfaces \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Source Kafka Interface",
    "productName": "KafkaSourceProduct",
    "interfaceType": 1,
    "description": "Source Kafka for testing",
    "productType": 1,
    "bootstrapServers": "kafka:9092",
    "topicName": "source-topic",
    "username": "",
    "password": ""
  }')

SOURCE_ID=$(echo $SOURCE_RESPONSE | jq -r '.interfaceId')
echo -e "${GREEN}✓ Source Interface ID: $SOURCE_ID${NC}"

# 2. Публикация Target Kafka интерфейса
echo -e "${BLUE}2. Публикация Target Kafka интерфейса...${NC}"
TARGET_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Interface \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Target Kafka Interface",
    "productName": "KafkaTargetProduct",
    "interfaceType": 1,
    "description": "Target Kafka for testing",
    "productType": 0,
    "bootstrapServers": "kafka3:9092",
    "topicName": "target-topic",
    "username": "",
    "password": ""
  }')

TARGET_ID=$(echo $TARGET_RESPONSE | jq -r '.interfaceId')
echo -e "${GREEN}✓ Target Interface ID: $TARGET_ID${NC}"

# 3. Создание интеграции
echo -e "${BLUE}3. Создание интеграции Kafka → Kafka...${NC}"
CONNECT_RESPONSE=$(curl -s -X POST http://localhost:5003/api/Subscription/connect \
  -H "Content-Type: application/json" \
  -d "{
    \"subscriptionInterfaceId\": $TARGET_ID,
    \"publicationInterfaceId\": $SOURCE_ID,
    \"integrationPattern\": \"8\",
    \"scheduleCron\": \"*/1 * * * *\",
    \"maxRetryAttempts\": 3,
    \"retryDelaySeconds\": 30,
    \"executionTimeoutSeconds\": 300
  }")

CONFIG_ID=$(echo $CONNECT_RESPONSE | jq -r '.orchestrationConfigId')
echo -e "${GREEN}✓ Orchestration Config ID: $CONFIG_ID${NC}"

# 4. Проверка создания коннектора Debezium
echo -e "${BLUE}4. Проверка Debezium коннектора...${NC}"
CONNECTOR_STATUS=$(curl -s http://localhost:8083/connectors/subscription-orchestration-connector/status 2>/dev/null)
if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Debezium коннектор уже существует${NC}"
else
    echo -e "${BLUE}   Создание Debezium коннектора...${NC}"
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
fi

# 5. Проверка созданных интерфейсов
echo -e "${BLUE}5. Проверка созданных интерфейсов...${NC}"
echo -e "${BLUE}   Source интерфейс (через Search API):${NC}"
curl -s "http://localhost:5002/api/Search/interfaces/by-id/$SOURCE_ID" | jq .

echo -e "${BLUE}   Consumer интерфейс (через Subscription API):${NC}"
curl -s "http://localhost:5003/api/Interface/$CONSUMER_ID" | jq .

echo -e "\n${GREEN}✅ Интеграция настроена!${NC}"
echo "Теперь вы можете:"
echo "1. Запустить TestProducer для отправки сообщений в source-topic"
echo "2. Запустить TestConsumer для получения сообщений из target-topic"
echo "3. Наблюдать за логами Engine: docker logs -f integration-engine"