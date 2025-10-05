using Confluent.Kafka;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class ApiToKafkaHandler(ILogger logger)
{
    private readonly ILogger _logger = logger;

    private async Task<string?> FetchDataFromApiAsync(DataInterface apiInterface)
    {
        if (apiInterface is not ApiInterface api)
        {
            _logger.LogError("Publication interface is not ApiInterface");
            return null;
        }

        try
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri($"{api.Host}:{api.Port}")
            };

            if (string.IsNullOrEmpty(api.Token))
            {
                _logger.LogWarning("No token received from API");
            }

            var response = await httpClient.GetAsync(api.Endpoint);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation(content);

            return content;
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            return null;
        }
    }

    private async Task SendDataToKafkaAsync(DataInterface kafkaInterface, string data)
    {
        if (kafkaInterface is not KafkaInterface kafka)
        {
            _logger.LogError("Kafka interface is not KafkaInterface");
            return;
        }

        var config = new ProducerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            SaslUsername = kafka.Username,
            SaslPassword = kafka.Password
        };

        using var producer = new ProducerBuilder<Null, string>(config).Build();

        try
        {
            var message = new Message<Null, string> { Value = data, };
            var deliveryResult = await producer.ProduceAsync(kafka.TopicName, message);

            _logger.LogInformation("Message sent to Kafka topic {Topic} at offset {Offset}",
                kafka.TopicName, deliveryResult.Offset);
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            throw;
        }
    }

    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface)
    {
        _logger.LogInformation("ApiToKafkaHandler.ExecuteAsync");

        var apiData = await FetchDataFromApiAsync(publicationInterface);
        if (apiData == null)
        {
            _logger.LogWarning("No data received from API");
            return;
        }

        await SendDataToKafkaAsync(subscriptionInterface, apiData);
    }
}