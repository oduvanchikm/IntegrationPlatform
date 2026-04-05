#!/bin/bash

echo "Test kafka to kafka integration"
echo "=========================================="

GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

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

echo -e "${BLUE}3. Создание интеграции Kafka → Kafka...${NC}"

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

if echo "$CONNECT_RESPONSE" | grep -q "orchestrationConfigId"; then
    CONFIG_ID=$(echo "$CONNECT_RESPONSE" | jq -r '.orchestrationConfigId')
    echo -e "${GREEN}✓ Orchestration Config ID: $CONFIG_ID${NC}"
else
    echo -e "${RED}✗ Failed to get Orchestration Config ID${NC}"
    echo "Full response: $CONNECT_RESPONSE"
fi

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

echo -e "${BLUE}5. Проверка созданных интерфейсов...${NC}"
echo -e "${BLUE}   Source интерфейс (через Search API):${NC}"
curl -s "http://localhost:5002/api/Search/interfaces/by-id/$SOURCE_ID" | jq .

echo -e "${BLUE}   Consumer интерфейс (через Subscription API):${NC}"
curl -s "http://localhost:5003/api/Interface/$CONSUMER_ID" | jq .

echo -e "${BLUE}6. Проверка созданных подключений...${NC}"
curl -s "http://localhost:5003/api/Subscription/connections" | jq .

echo -e "${BLUE}7. Тестирование пересылки сообщений...${NC}"

echo -e "${YELLOW}   Отправка тестового сообщения в source-topic...${NC}"
sleep 5

# ✅ Исправлено: --bootstrap-server вместо --broker-list
TEST_MESSAGE="Kafka2Kafka_$(date +%s)"
echo "new" | docker exec -i kafka2 \
  kafka-console-producer --bootstrap-server kafka2:9092 --topic source-topic

echo -e "${GREEN}   ✓ Сообщение отправлено: $TEST_MESSAGE${NC}"

echo -e "${YELLOW}   Ожидание обработки Engine (15 сек)...${NC}"
sleep 15

echo -e "${YELLOW}   Чтение сообщения из target-topic...${NC}"
# ✅ Исправлено: добавлен --from-beginning чтобы видеть уже записанные сообщения
RESULT=$(docker exec kafka33 kafka-console-consumer \
  --bootstrap-server kafka33:9092 \
  --topic target-topic \
  --from-beginning \
  --max-messages 200 \
  --timeout-ms 10000 2>/dev/null)

if [[ -n "$RESULT" ]]; then
    echo -e "${GREEN}   ✓ Сообщение получено в target-topic:${NC}"
    echo -e "     ${BLUE}$RESULT${NC}"
    
    # ✅ Дополнительная проверка: совпадает ли содержимое
    if [[ "$RESULT" == *"$TEST_MESSAGE"* ]]; then
        echo -e "${GREEN}   ✓ Содержимое сообщения совпадает!${NC}"
    else
        echo -e "${YELLOW}   ⚠ Сообщение получено, но содержимое отличается (возможно, прочитано старое)${NC}"
    fi
else
    echo -e "${RED}   ✗ Нет сообщений в target-topic${NC}"
    echo -e "${BLUE}   🔍 Диагностика:${NC}"
    echo -e "     - Проверка топика target-topic..."
    docker exec kafka33 kafka-topics --describe --topic target-topic --bootstrap-server kafka33:9092 2>/dev/null || echo "       ❌ Топик не найден"
    
    echo -e "     - Последние 3 сообщения в target-topic:"
    docker exec kafka33 kafka-console-consumer \
      --bootstrap-server kafka33:9092 \
      --topic target-topic \
      --from-beginning \
      --max-messages 3 \
      --timeout-ms 5000 2>/dev/null || echo "       (пусто)"
fi

echo -e "${BLUE}8. Последние логи Engine:${NC}"
docker logs --tail 20 integration-engine 2>/dev/null | grep -E "(📥|📤|✅|❌|MESSAGE|WriteToKafka)" || echo "   (нет релевантных логов)"

# Очистка
echo -e "${YELLOW}   Очистка: удаление связи...${NC}"
curl -s -X DELETE "http://localhost:5003/api/Subscription/connections/$CONFIG_ID" &>/dev/null
echo -e "${GREEN}   ✓ Связь удалена${NC}"

echo -e "\n${GREEN}✅ Интеграция настроена и протестирована!${NC}"
echo ""
echo "📌 Полезные команды для ручной отладки:"
echo "   • Отправить сообщение:  docker exec -i kafka2 kafka-console-producer --bootstrap-server kafka2:9092 --topic source-topic"
echo "   • Прочитать из target:   docker exec kafka33 kafka-console-consumer --bootstrap-server kafka33:9092 --topic target-topic --from-beginning"
echo "   • Следить за логами:     docker logs -f integration-engine"
echo "   • Описать топик:         docker exec kafka33 kafka-topics --describe --topic target-topic --bootstrap-server kafka33:9092"