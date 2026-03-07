using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Engine.Applications.Orchestration;

public class OrchestrationService
{
    private readonly ILogger<OrchestrationService> _logger;
    private readonly IDbContextFactory<PublicationDbContext> _publicationDbContext;
    private readonly IDbContextFactory<SubscriptionDbContext> _subscriptionDbContext;
    private readonly IServiceProvider _serviceProvider;
    
    public OrchestrationService(
        ILogger<OrchestrationService> logger,
        IDbContextFactory<PublicationDbContext> publicationDbContext,
        IDbContextFactory<SubscriptionDbContext> subscriptionDbContext,
        IServiceProvider serviceProvider) 
    {
        _logger = logger;
        _publicationDbContext = publicationDbContext;
        _subscriptionDbContext = subscriptionDbContext;
        _serviceProvider = serviceProvider;
    }


    public async Task HandleNewOrchestration(OrchestrationConfigModel config)
    {
        await using var publicationDb = await _publicationDbContext.CreateDbContextAsync();
        await using var subscriptionDb = await _subscriptionDbContext.CreateDbContextAsync();

        var publicationInterface = await publicationDb.DataInterfaces
            .Include(d => d.Product)
            .FirstOrDefaultAsync(d => d.Id == config.InterfacePublicationId);

        var subscriptionInterface = await subscriptionDb.DataInterfaces
            .Include(d => d.Product)
            .Include(d => d.OrchestrationConfig)
            .FirstOrDefaultAsync(d => d.Id == config.InterfaceSubscriptionId);

        if (publicationInterface == null || subscriptionInterface == null)
        {
            _logger.LogError("Cannot find interfaces for IDs {PubId} / {SubId}", config.InterfacePublicationId,
                config.InterfaceSubscriptionId);
            return;
        }


        await HandleIntegration(config.IntegrationPattern, publicationInterface, subscriptionInterface, config);
    }

    private async Task HandleIntegration(string integrationPattern, DataInterface publicationInterface,
        DataInterface subscriptionInterface,
        OrchestrationConfigModel config)
    {
        try
        {
            switch (integrationPattern)
            {
                case "ApiToDatabase":
                    _logger.LogInformation("ApiToDatabase");
                    break;
                case "ApiToKafka":
                    _logger.LogInformation("ApiToKafka");
                    await new ApiToKafkaHandler(_logger)
                        .ExecuteAsync(publicationInterface, subscriptionInterface);
                    break;
                case "ApiToApi":
                    _logger.LogInformation("ApiToApi");
                    break;
                case "KafkaToApi":
                    _logger.LogInformation("KafkaToApi");
                    await new KafkaToApiHandler(_logger)
                        .ExecuteAsync(publicationInterface, subscriptionInterface);
                    break;
                case "KafkaToKafka":
                    _logger.LogInformation("KafkaToKafka");
                    await new KafkaToKafkaHandler(_logger)
                        .ExecuteAsync(publicationInterface, subscriptionInterface);
                    break;
                case "KafkaToDatabase":
                    _logger.LogInformation("KafkaToDatabase");
                    var kafkaToApiHandler = ActivatorUtilities.CreateInstance<KafkaToApiHandler>(_serviceProvider);
                    await kafkaToApiHandler.ExecuteAsync(publicationInterface, subscriptionInterface);
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