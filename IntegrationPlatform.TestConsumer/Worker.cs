using Confluent.Kafka;

namespace IntegrationPlatform.TestConsumer;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() =>
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = "localhost:9092",
                GroupId = "test-consumer",
                AutoOffsetReset = AutoOffsetReset.Earliest
            };

            using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();

            consumer.Subscribe("target-topic");

            _logger.LogInformation("Kafka Test Consumer started");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var result = consumer.Consume(stoppingToken);

                    Console.WriteLine($"Received message: {result.Message.Value}");
                }
            }
            catch (OperationCanceledException)
            {
                consumer.Close();
            }
        }, stoppingToken);
    }
}