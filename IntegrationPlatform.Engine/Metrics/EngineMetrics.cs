using Prometheus;

namespace IntegrationPlatform.Engine.Metrics;

public static class EngineMetrics
{
    // Количество обработанных событий
    public static readonly Counter EventsProcessed = Prometheus.Metrics
        .CreateCounter("engine_events_processed_total", 
            "Total number of events processed by engine",
            new CounterConfiguration { LabelNames = new[] { "pattern" } });
    
    // Количество ошибок в обработке
    public static readonly Counter ProcessingErrors = Prometheus.Metrics
        .CreateCounter("engine_processing_errors_total", 
            "Total number of processing errors",
            new CounterConfiguration { LabelNames = new[] { "pattern", "error_type" } });
    
    // Время обработки события
    public static readonly Histogram ProcessingDuration = Prometheus.Metrics
        .CreateHistogram("engine_processing_duration_seconds", 
            "Duration of event processing",
            new HistogramConfiguration 
            { 
                Buckets = new[] { 0.1, 0.5, 1, 2, 5, 10 },
                LabelNames = new[] { "pattern" }
            });
    
    // Количество активных задач (стримов)
    public static readonly Gauge ActiveTasks = Prometheus.Metrics
        .CreateGauge("engine_active_tasks", 
            "Number of active streaming tasks");
    
    // Размер очереди сообщений Kafka
    public static readonly Gauge KafkaLag = Prometheus.Metrics
        .CreateGauge("engine_kafka_consumer_lag", 
            "Kafka consumer lag by topic",
            new GaugeConfiguration { LabelNames = new[] { "topic" } });
    
    public static readonly Counter MessagesConsumed = Prometheus.Metrics
        .CreateCounter("engine_kafka_messages_consumed_total",
            "Total messages consumed from Kafka",
            new CounterConfiguration { LabelNames = new[] { "topic" } });
    
    public static readonly Counter ConsumerErrors = Prometheus.Metrics
        .CreateCounter("engine_kafka_consumer_errors_total",
            "Total consumer errors",
            new CounterConfiguration { LabelNames = new[] { "topic", "error_type" } });
}