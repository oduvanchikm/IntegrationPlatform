namespace IntegrationPlatform.Common.Enums;

public enum InterfaceType
{
    Db,
    Kafka,
    Api
}

public enum ProductType
{
    Consumer,
    Source
}

public enum ConnectionStatus
{
    Draft,
    Active,
    Deprecated
}

public enum IntegrationPattern
{
    DatabaseToDatabase,
    DatabaseToApi,
    DatabaseToKafka,
    ApiToDatabase,
    ApiToKafka,
    ApiToApi,
    KafkaToDatabase,
    KafkaToApi,
    KafkaToKafka
}