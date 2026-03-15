using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Engine.Applications.Orchestration;

public class OrchestrationService(
    ILogger<OrchestrationService> logger,
    IDbContextFactory<PublicationDbContext> publicationDbContext,
    IDbContextFactory<SubscriptionDbContext> subscriptionDbContext,
    IServiceProvider serviceProvider)
{
    public async Task HandleNewOrchestration(OrchestrationConfigModel config)
    {
        logger.LogInformation("========== HANDLE NEW ORCHESTRATION ==========");
        logger.LogInformation("Processing config ID: {ConfigId}", config.Id);
        logger.LogInformation("Publication ID: {PubId}, Subscription ID: {SubId}, Pattern: {Pattern}",
            config.InterfacePublicationId, config.InterfaceSubscriptionId, config.IntegrationPattern);

        await using var publicationDb = await publicationDbContext.CreateDbContextAsync();
        await using var subscriptionDb = await subscriptionDbContext.CreateDbContextAsync();

        logger.LogInformation("Fetching publication interface with ID: {PubId}", config.InterfacePublicationId);
        var publicationInterface = await publicationDb.DataInterfaces
            .Include(d => d.Product)
            .FirstOrDefaultAsync(d => d.Id == config.InterfacePublicationId);

        logger.LogInformation("Fetching subscription interface with ID: {SubId}", config.InterfaceSubscriptionId);
        var subscriptionInterface = await subscriptionDb.DataInterfaces
            .Include(d => d.Product)
            .Include(d => d.OrchestrationConfig)
            .FirstOrDefaultAsync(d => d.Id == config.InterfaceSubscriptionId);

        if (publicationInterface == null)
        {
            logger.LogError("Publication interface {PubId} NOT FOUND in database!", config.InterfacePublicationId);
            return;
        }

        if (subscriptionInterface == null)
        {
            logger.LogError("Subscription interface {SubId} NOT FOUND in database!", config.InterfaceSubscriptionId);
            return;
        }

        logger.LogInformation("Publication interface found: ID={Id}, Name={Name}, Type={Type}",
            publicationInterface.Id, publicationInterface.Name, publicationInterface.InterfaceType);
        logger.LogInformation("Subscription interface found: ID={Id}, Name={Name}, Type={Type}",
            subscriptionInterface.Id, subscriptionInterface.Name, subscriptionInterface.InterfaceType);

        await HandleIntegration(config.IntegrationPattern, publicationInterface, subscriptionInterface, config);

        logger.LogInformation("========== HANDLE NEW ORCHESTRATION END ==========");
    }

    private async Task HandleIntegration(string integrationPattern, DataInterface publicationInterface,
        DataInterface subscriptionInterface, OrchestrationConfigModel config)
    {
        logger.LogInformation("HandleIntegration called with pattern: {Pattern}", integrationPattern);

        try
        {
            switch (integrationPattern)
            {
                case "ApiToDatabase":
                    logger.LogInformation("ApiToDatabase handler not implemented yet");
                    break;

                case "ApiToKafka":
                    logger.LogInformation("Creating ApiToKafkaHandler");
                    var apiToKafkaHandler = ActivatorUtilities.CreateInstance<ApiToKafkaHandler>(serviceProvider);
                    await apiToKafkaHandler.ExecuteAsync(publicationInterface, subscriptionInterface);
                    break;

                case "ApiToApi":
                    logger.LogInformation("ApiToApi handler not implemented yet");
                    break;

                case "KafkaToApi":
                    logger.LogInformation("Creating KafkaToApiHandler");
                    var kafkaToApiHandler = ActivatorUtilities.CreateInstance<KafkaToApiHandler>(serviceProvider);
                    await kafkaToApiHandler.ExecuteAsync(publicationInterface, subscriptionInterface);
                    break;

                case "KafkaToKafka":
                    logger.LogInformation("========== EXECUTING KAFKA TO KAFKA ==========");
                    logger.LogInformation("Creating KafkaToKafkaHandler with DI");

                    // ВАЖНО: Используем DI для создания хендлера
                    var kafkaToKafkaHandler = ActivatorUtilities.CreateInstance<KafkaToKafkaHandler>(serviceProvider);

                    logger.LogInformation("Calling KafkaToKafkaHandler.ExecuteAsync");
                    await kafkaToKafkaHandler.ExecuteAsync(publicationInterface, subscriptionInterface);

                    logger.LogInformation("========== KAFKA TO KAFKA EXECUTION COMPLETE ==========");
                    break;

                case "KafkaToDatabase":
                    logger.LogInformation("Creating KafkaToDatabaseHandler");
                    var kafkaToDatabaseHandler =
                        ActivatorUtilities.CreateInstance<KafkaToDatabaseHandler>(serviceProvider);
                    await kafkaToDatabaseHandler.ExecuteAsync(publicationInterface, subscriptionInterface);
                    break;

                case "DatabaseToApi":
                    logger.LogInformation("DatabaseToApi handler not implemented yet");
                    break;

                case "DatabaseToKafka":
                    logger.LogInformation("DatabaseToKafka handler not implemented yet");
                    break;

                case "DatabaseToDatabase":
                    logger.LogInformation("DatabaseToDatabase handler not implemented yet");
                    break;

                default:
                    logger.LogWarning("Unknown integration pattern: {Pattern}", integrationPattern);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in HandleIntegration for pattern {Pattern}", integrationPattern);
        }
    }
}