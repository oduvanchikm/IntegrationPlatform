using Prometheus;

namespace IntegrationPlatform.Search.API.Metrics;

public static class SearchMetrics
{
    public static readonly Counter SearchQueriesTotal = Prometheus.Metrics
        .CreateCounter("search_queries_total",
            "Total number of search queries");
    
    public static readonly Histogram SearchDuration = Prometheus.Metrics
        .CreateHistogram("search_duration_seconds",
            "Duration of search queries",
            new HistogramConfiguration
            {
                Buckets = new[] { 0.01, 0.05, 0.1, 0.5, 1 }
            });
}