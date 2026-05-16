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

    public static readonly Gauge ApiInterfaces = Prometheus.Metrics
        .CreateGauge("subscription_api_interfaces",
            "Number of API type interfaces");

    public static readonly Gauge KafkaInterfaces = Prometheus.Metrics
        .CreateGauge("subscription_kafka_interfaces",
            "Number of Kafka type interfaces");

    public static readonly Gauge DatabaseInterfaces = Prometheus.Metrics
        .CreateGauge("subscription_database_interfaces",
            "Number of Database type interfaces");

    public static readonly Gauge TotalOrchestrationConfigs = Prometheus.Metrics
        .CreateGauge("subscription_orchestration_configs_total",
            "Total number of orchestration configurations");

    public static readonly Counter InterfacesCreated = Prometheus.Metrics
        .CreateCounter("subscription_interfaces_created_total",
            "Total number of consumer interfaces created",
            new CounterConfiguration { LabelNames = ["interface_type"] });

    public static readonly Gauge ActiveConsumerInterfaces = Prometheus.Metrics
        .CreateGauge("subscription_active_consumer_interfaces",
            "Number of active consumer interfaces");

    public static readonly Counter ErrorsTotal = Prometheus.Metrics
        .CreateCounter("subscription_interface_errors_total",
            "Total number of errors in interface controller",
            new CounterConfiguration { LabelNames = ["error_type"] });

    public static readonly Histogram RequestDuration = Prometheus.Metrics
        .CreateHistogram("subscription_interface_request_duration_seconds",
            "Duration of interface API requests",
            new HistogramConfiguration { Buckets = [0.01, 0.05, 0.1, 0.5, 1, 2, 5] });

    public static readonly Counter SubscriptionsDeleted = Prometheus.Metrics
        .CreateCounter("subscriptions_deleted_total", "Total subscriptions deleted");
}