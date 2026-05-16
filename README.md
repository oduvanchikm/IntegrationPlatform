# Интеграционная платформа

Интеграционная платформа для обмена данными между системами с разнородными интерфейсами (API, Apache Kafka, PostgreSQL).

---

## Возможности

- 9 паттернов интеграции:
    - Database → Database, Kafka, API
    - API → Database, Kafka, API
    - Kafka → Database, API, Kafka
- Автоматическое определение паттерна по типам интерфейсов
- Настройка через UI
- Change Data Capture (CDC) через Debezium
- Мониторинг и логирование (Prometheus + Grafana + Loki)
- Контейнеризация (Docker + Docker Compose)

---

## Технологии

| Компонент | Технология |
|-----------|------------|
| Язык | C# (.NET 8) |
| Брокер сообщений | Apache Kafka |
| CDC | Debezium |
| База данных | PostgreSQL |
| Мониторинг | Prometheus, Grafana, Loki, Node Exporter, cAdvisor |
| Контейнеризация | Docker, Docker Compose |
| Интерфейс | HTML, CSS, JavaScript |

---

## Быстрый старт

```bash
git clone https://github.com/oduvanchikm/IntegrationPlatform.git
cd IntegrationPlatform
docker-compose up -d
```

## Swagger документация
- Publication API: ```http://localhost:5001/swagger```
- Search API: ```http://localhost:5002/swagger```
- Subscription API: ```http://localhost:5003/swagger```

