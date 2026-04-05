using Prometheus;

namespace IntegrationPlatform.Subscription.API.Metrics;

public static class SubscriptionMetrics
{
    public static readonly Counter SubscriptionsCreated = Prometheus.Metrics
        .CreateCounter("subscriptions_created_total", 
            "Total number of subscriptions created");
    
    public static readonly Gauge ActiveSubscriptions = Prometheus.Metrics
        .CreateGauge("subscriptions_active_total", 
            "Number of active subscriptions");
    
    public static readonly Gauge TotalProducts = Prometheus.Metrics
        .CreateGauge("subscription_products_total", 
            "Total number of products in subscription database");
    
    public static readonly Gauge TotalInterfaces = Prometheus.Metrics
        .CreateGauge("subscription_total_interfaces", 
            "Total number of interfaces in subscription database (all statuses)");
    
    public static readonly Gauge DraftInterfaces = Prometheus.Metrics
        .CreateGauge("subscription_draft_interfaces", 
            "Number of draft interfaces");
    
    public static readonly Gauge DeprecatedInterfaces = Prometheus.Metrics
        .CreateGauge("subscription_deprecated_interfaces", 
            "Number of deprecated interfaces");
    
    // Распределение по типам интерфейсов
    public static readonly Gauge ApiInterfaces = Prometheus.Metrics
        .CreateGauge("subscription_api_interfaces", 
            "Number of API type interfaces");
    
    public static readonly Gauge KafkaInterfaces = Prometheus.Metrics
        .CreateGauge("subscription_kafka_interfaces", 
            "Number of Kafka type interfaces");
    
    public static readonly Gauge DatabaseInterfaces = Prometheus.Metrics
        .CreateGauge("subscription_database_interfaces", 
            "Number of Database type interfaces");
    
    // Метрики для оркестрации
    public static readonly Gauge TotalOrchestrationConfigs = Prometheus.Metrics
        .CreateGauge("subscription_orchestration_configs_total", 
            "Total number of orchestration configurations");
}