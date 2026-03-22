using System.Text;
using Confluent.Kafka;
using IntegrationPlatform.Common.Models;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class KafkaToApiHandler(ILogger logger, IServiceProvider serviceProvider)
{
    private readonly ILogger _logger = logger;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    
    private async Task<List<string>> ConsumerFromSourceKafkaAsync(DataInterface dataInterface, int batchSize = 100)
    {
        if (dataInterface is not KafkaInterface kafkaInterface)
        {
            _logger.LogError("Source interface is not Kafka Interface");
            return new List<string>();
        }

        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaInterface.BootstrapServers,
            GroupId = $"kafka-to-kafka-{Guid.NewGuid()}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            SessionTimeoutMs = 30000,
            MaxPollIntervalMs = 300000
        };

        var message = new List<string>();

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(kafkaInterface.TopicName);

        _logger.LogInformation($"Consumer subscribed to topic {kafkaInterface.TopicName}");

        try
        {
            var timeout = TimeSpan.FromSeconds(30);
            var startTime = DateTime.UtcNow;

            while (message.Count < batchSize && (DateTime.UtcNow - startTime) < timeout)
            {
                var consumeResult = consumer.Consume(timeout);

                if (consumeResult == null) continue;

                message.Add(consumeResult.Message.Value);
                _logger.LogDebug("Consumed message from offset: {Offset}", consumeResult.Offset);

                consumer.Commit(consumeResult);
            }

            _logger.LogInformation("Consumed {Count} messages from {Topic}", message.Count, kafkaInterface.TopicName);
        }
        catch (ConsumeException e)
        {
            _logger.LogError($"Error occured: {e.Error.Reason}");
            Console.WriteLine(e);
            throw;
        }
        finally
        {
            consumer.Close();
        }

        return message;
    }

    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface)
    { 
        _logger.LogInformation("========== KAFKA TO API HANDLER EXECUTE START ==========");

        var sourceKafka = publicationInterface as KafkaInterface;
        var targetApi = subscriptionInterface as ApiInterface;

        _logger.LogInformation("Publication interface ID: {Id}, Name: {Name}",
            publicationInterface.Id, publicationInterface.Name);
        _logger.LogInformation("Subscription interface ID: {Id}, Name: {Name}",
            subscriptionInterface.Id, subscriptionInterface.Name);
        
        if (sourceKafka == null)
        {
            _logger.LogError("Publication interface is not KafkaInterface! Actual type: {Type}",
                publicationInterface.GetType().Name);
            return;
        }

        if (targetApi == null)
        {
            _logger.LogError("Subscription interface is not ApiInterface! Actual type: {Type}",
                subscriptionInterface.GetType().Name);
            return;
        }
        
        _logger.LogInformation("Source Kafka: BootstrapServers={BS}, Topic={Topic}",
            sourceKafka.BootstrapServers, sourceKafka.TopicName);
        _logger.LogInformation("Target Kafka: Endpoint={BS}, Host={Topic}, Port={Port}",
            targetApi.Endpoint, targetApi.Host, targetApi.Port);
        
        try
        {
            _logger.LogInformation("Attempting to consume from source topic...");
            var messages = await ConsumerFromSourceKafkaAsync(publicationInterface, batchSize: 50);
            _logger.LogInformation("Consumed {Count} messages from source topic", messages.Count);

            if (!messages.Any())
            {
                _logger.LogWarning("No messages found in source topic. Will retry later.");
                return;
            }

            _logger.LogInformation("Attempting to send {Count} messages to target topic", messages.Count);
            
            using var httpClient = new HttpClient();
            foreach (var message in messages)
            {
                var content = new StringContent(message, Encoding.UTF8, "application/json");
                await httpClient.PostAsync($"{targetApi.Host}:{targetApi.Port}{targetApi.Endpoint}", content);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in KafkaToKafkaHandler.ExecuteAsync: {Message}", ex.Message);
        }

        _logger.LogInformation("========== KAFKA TO API HANDLER EXECUTE END ==========");
    }
}