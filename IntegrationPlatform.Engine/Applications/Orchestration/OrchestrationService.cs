using IntegrationPlatform.Common.Enums;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Engine.Applications.Orchestration;

public abstract class OrchestrationService(
    ILogger<OrchestrationService> logger,
    IDbContextFactory<PublicationDbContext> publicationDbContext,
    IDbContextFactory<PublicationDbContext> subscriptionDbContext)
{
    private readonly ILogger<OrchestrationService> _logger = logger;
    private readonly IDbContextFactory<PublicationDbContext> _publicationDbContext = publicationDbContext;
    private readonly IDbContextFactory<PublicationDbContext> _subscriptionDbContext = subscriptionDbContext;

    public async Task HandleNewOrchestration(OrchestrationConfigModel config)
    {
        await using var publicationDb = await _publicationDbContext.CreateDbContextAsync();
        await using var subscriptionDb = await _subscriptionDbContext.CreateDbContextAsync();

        var pub = await publicationDb.DataInterfaces.FindAsync(config.InterfacePublicationId);
        var sub = await subscriptionDb.DataInterfaces.FindAsync(config.InterfaceSubscriptionId);

        try
        {
            switch (config.IntegrationPattern)
            {
                case "ApiToDatabase":
                    _logger.LogInformation("ApiToDatabase");
                    break;
                case "ApiToKafka":
                    _logger.LogInformation("ApiToKafka");
                    await new ApiToKafkaHandler(_logger).ExecuteAsync(pub, sub);
                    break;
                case "ApiToApi":
                    _logger.LogInformation("ApiToApi");
                    break;
                case "KafkaToApi":
                    _logger.LogInformation("KafkaToApi");
                    break;
                case "KafkaToKafka":
                    _logger.LogInformation("KafkaToKafka");
                    break;
                case "KafkaToDatabase":
                    _logger.LogInformation("KafkaToDatabase");
                    break;
                case "DatabaseToApi":
                    _logger.LogInformation("DatabaseToApi");
                    break;
                case "DatabaseToKafka":
                    _logger.LogInformation("DatabaseToKafka");
                    break;
                case "DatabaseToDatabase":
                    _logger.LogInformation("DatabaseToDatabase");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}