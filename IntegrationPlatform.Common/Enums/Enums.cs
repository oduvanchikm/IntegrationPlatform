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

// public enum OrchestrationType
// {
//     KubernetesCronJob,
//     KubernetesDeployment,
//     AirflowDAG,
//     CustomScript
// }

public enum IntegrationPattern
{
    DatabaseToDatabase,
    DatabaseToApi,
    DatabaseToKafka,
    ApiToDatabase,
    ApiToKafka,
    KafkaToDatabase,
    KafkaToApi
}