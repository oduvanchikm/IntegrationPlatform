using Confluent.Kafka;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class ApiToKafkaHandler(ILogger logger, IServiceProvider serviceProvider)
{
    private readonly ILogger _logger = logger;
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    private async Task SendToTargetKafkaAsync(DataInterface dataInterface, List<string> kafkaMessages)
    {
        if (dataInterface is not KafkaInterface targetKafka)
        {
            _logger.LogError("Target interface is not KafkaInterface");
            return;
        }

        if (!kafkaMessages.Any())
        {
            _logger.LogInformation("No messages to send to target Kafka");
            return;
        }

        var config = new ProducerConfig
        {
            BootstrapServers = targetKafka.BootstrapServers,
            SaslUsername = targetKafka.Username,
            SaslPassword = targetKafka.Password,
            LingerMs = 5,
            BatchSize = 16384
        };

        using var producer = new ProducerBuilder<Null, string>(config)
            .SetErrorHandler((_, error) =>
            {
                _logger.LogError("Kafka producer error: {Code} - {Reason}", error.Code, error.Reason);
            })
            .Build();

        var successCount = 0;
        var failCount = 0;

        try
        {
            foreach (var message in kafkaMessages)
            {
                try
                {
                    var kafkaMessage = new Message<Null, string>
                    {
                        Value = message
                    };

                    var deliveryResult = await producer.ProduceAsync(targetKafka.TopicName, kafkaMessage);
                    successCount++;

                    _logger.LogDebug("Message sent to target Kafka topic {Topic} at offset {Offset}",
                        targetKafka.TopicName, deliveryResult.Offset);
                }
                catch (ProduceException<Null, string> ex)
                {
                    failCount++;
                    _logger.LogError(ex, "Failed to send message to target Kafka: {Error}", ex.Error.Reason);
                }
            }

            _logger.LogInformation(
                "Successfully sent {SuccessCount}/{TotalCount} messages to target Kafka topic: {Topic}",
                successCount, kafkaMessages.Count, targetKafka.TopicName);

            if (failCount > 0)
            {
                _logger.LogWarning("Failed to send {FailCount} messages", failCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while sending to target Kafka");
            throw;
        }
    }

    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface)
    {
        _logger.LogInformation("========== API TO KAFKA HANDLER EXECUTE START ==========");

        var sourceApi = publicationInterface as ApiInterface;
        var targetKafka = subscriptionInterface as KafkaInterface;

        _logger.LogInformation("Publication interface ID: {Id}, Name: {Name}",
            publicationInterface.Id, publicationInterface.Name);
        _logger.LogInformation("Subscription interface ID: {Id}, Name: {Name}",
            subscriptionInterface.Id, subscriptionInterface.Name);

        if (sourceApi == null)
        {
            _logger.LogError("Publication interface is not KafkaInterface! Actual type: {Type}",
                publicationInterface.GetType().Name);
            return;
        }

        if (targetKafka == null)
        {
            _logger.LogError("Subscription interface is not KafkaInterface! Actual type: {Type}",
                subscriptionInterface.GetType().Name);
            return;
        }

        _logger.LogInformation("Source API: Endpoint={Endpoint}, Host={Host}, Port={Port}",
            sourceApi.Endpoint, sourceApi.Host, sourceApi.Port);
        _logger.LogInformation("Target Kafka: BootstrapServers={BS}, Topic={Topic}",
            targetKafka.BootstrapServers, targetKafka.TopicName);

        try
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"{sourceApi.Host}:{sourceApi.Port}{sourceApi.Endpoint}");
            var messages = await response.Content.ReadAsStringAsync();

            await SendToTargetKafkaAsync(subscriptionInterface, new List<string> { messages });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in KafkaToKafkaHandler.ExecuteAsync: {Message}", ex.Message);
        }

        _logger.LogInformation("========== API TO KAFKA HANDLER EXECUTE END ==========");
    }
}