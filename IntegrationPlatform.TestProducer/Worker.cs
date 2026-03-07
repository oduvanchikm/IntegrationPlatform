using Confluent.Kafka;

namespace IntegrationPlatform.TestProducer;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = "localhost:9092"
        };

        using var producer = new ProducerBuilder<Null, string>(config).Build();

        _logger.LogInformation("Kafka Test Producer started");

        while (!stoppingToken.IsCancellationRequested)
        {
            Console.Write("Enter message: ");
            var message = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(message))
                continue;

            try
            {
                var result = await producer.ProduceAsync(
                    "source-topic",
                    new Message<Null, string> { Value = message },
                    stoppingToken);

                _logger.LogInformation(
                    "Message sent to topic {Topic} offset {Offset}",
                    result.Topic,
                    result.Offset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
            }
        }
    }
}