using Prometheus;

namespace IntegrationPlatform.Search.API.Metrics;

public static class SearchMetrics
{
    public static readonly Counter SearchQueriesTotal = Prometheus.Metrics
        .CreateCounter("search_queries_total",
            "Total number of search queries",
            new CounterConfiguration { LabelNames = new[] { "query_type" } });
    
    public static readonly Histogram SearchDuration = Prometheus.Metrics
        .CreateHistogram("search_duration_seconds",
            "Duration of search queries",
            new HistogramConfiguration
            {
                LabelNames = new[] { "query_type" },
                Buckets = new double[] { 0.01, 0.05, 0.1, 0.5, 1 }
            });
    
    public static readonly Histogram SearchResultsCount = Prometheus.Metrics
        .CreateHistogram("search_results_count",
            "Number of results returned by search",
            new HistogramConfiguration
            {
                Buckets = new double[] { 0, 1, 5, 10, 25, 50, 100 }
            });
}