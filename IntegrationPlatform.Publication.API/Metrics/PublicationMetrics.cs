using Prometheus;

namespace IntegrationPlatform.Publication.API.Metrics;

public static class PublicationMetrics
{
    public static readonly Counter InterfacesPublished = Prometheus.Metrics
        .CreateCounter("publication_interfaces_published_total", 
            "Total number of interfaces published");
    
    public static readonly Gauge ActiveSourceInterfaces = Prometheus.Metrics
        .CreateGauge("publication_active_source_interfaces", 
            "Number of active source interfaces");
    
    public static readonly Counter ErrorsTotal = Prometheus.Metrics
        .CreateCounter("publication_errors_total", 
            "Total number of errors");
    
    public static readonly Gauge TotalProducts = Prometheus.Metrics
        .CreateGauge("publication_products_total", 
            "Total number of products in publication database");
    
    public static readonly Gauge TotalInterfaces = Prometheus.Metrics
        .CreateGauge("publication_total_interfaces", 
            "Total number of interfaces in publication database (all statuses)");
    
    public static readonly Gauge DraftInterfaces = Prometheus.Metrics
        .CreateGauge("publication_draft_interfaces", 
            "Number of draft interfaces");
    
    public static readonly Gauge DeprecatedInterfaces = Prometheus.Metrics
        .CreateGauge("publication_deprecated_interfaces", 
            "Number of deprecated interfaces");
    
    public static readonly Gauge ApiInterfaces = Prometheus.Metrics
        .CreateGauge("publication_api_interfaces", 
            "Number of API type interfaces");
    
    public static readonly Gauge KafkaInterfaces = Prometheus.Metrics
        .CreateGauge("publication_kafka_interfaces", 
            "Number of Kafka type interfaces");
    
    public static readonly Gauge DatabaseInterfaces = Prometheus.Metrics
        .CreateGauge("publication_database_interfaces", 
            "Number of Database type interfaces");
}