using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;
using IntegrationPlatform.Engine.Metrics;
using Prometheus;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class KafkaToApiHandler(ILogger<KafkaToApiHandler> logger, KafkaReader kafkaReader, ApiWriter apiWriter)
{
    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("========== KAFKA TO API HANDLER EXECUTE START ==========");
        var pattern = "KafkaToApi";
        using var timer = EngineMetrics.ProcessingDuration.WithLabels(pattern).NewTimer();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();


            var sourceKafka = publicationInterface as KafkaInterface;
            var targetApi = subscriptionInterface as ApiInterface;

            if (sourceKafka == null || targetApi == null)
            {
                logger.LogError("Kafka and API are required.");
                return;
            }

            logger.LogInformation("Source Kafka: BootstrapServers={BS}, Topic={Topic}",
                sourceKafka.BootstrapServers, sourceKafka.TopicName);
            logger.LogInformation("Target Kafka: Endpoint={Endpoint}, Host={Host}, Port={Port}",
                targetApi.Endpoint, targetApi.Host, targetApi.Port);

            await kafkaReader.ReadFromKafkaAsync(sourceKafka, async (message) =>
            {
                logger.LogInformation("Received message from Kafka, writing to API");
                await apiWriter.WriteToApiAsync(targetApi, new List<string> { message });
                logger.LogDebug("Message written to API");
            }, cancellationToken);
            EngineMetrics.EventsProcessed.WithLabels(pattern).Inc();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Kafka and API are required.");
            EngineMetrics.ProcessingErrors.WithLabels(pattern, "execution_error").Inc();
            throw;
        }
    }
}